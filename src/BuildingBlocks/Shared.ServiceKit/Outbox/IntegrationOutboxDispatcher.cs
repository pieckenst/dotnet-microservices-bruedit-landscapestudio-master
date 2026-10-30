using System.Text.Json;
using EventBus.Messages.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Common.Outbox;

namespace Shared.ServiceKit.Outbox;

/// <summary>
/// Delivers committed outbox messages to RabbitMQ at least once. A lease and optimistic
/// concurrency token prevent two service instances from claiming the same row concurrently.
/// A crash after broker acceptance but before marking the row may cause redelivery, so
/// consumers must remain idempotent.
/// </summary>
public sealed class IntegrationOutboxDispatcher<TContext>(
    IServiceScopeFactory scopeFactory,
    ILogger<IntegrationOutboxDispatcher<TContext>> logger) : BackgroundService
    where TContext : DbContext
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly IReadOnlyDictionary<string, Type> MessageTypes =
        typeof(IntegrationBaseEvent).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(IntegrationBaseEvent).IsAssignableFrom(type))
            .ToDictionary(type => type.FullName!, StringComparer.Ordinal);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var foundWork = await DispatchOneAsync(stoppingToken);
                if (!foundWork)
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Integration outbox worker failed; it will retry.");
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
        }
    }

    private async Task<bool> DispatchOneAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var now = DateTime.UtcNow;

        var id = await context.Set<IntegrationOutboxMessage>()
            .AsNoTracking()
            .Where(message =>
                message.PublishedAtUtc == null &&
                message.DeadLetteredAtUtc == null &&
                message.NextAttemptAtUtc <= now &&
                (message.LeaseExpiresAtUtc == null || message.LeaseExpiresAtUtc <= now))
            .OrderBy(message => message.NextAttemptAtUtc)
            .ThenBy(message => message.CreatedAtUtc)
            .Select(message => message.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (id == Guid.Empty)
            return false;

        var record = await context.Set<IntegrationOutboxMessage>()
            .SingleOrDefaultAsync(message => message.Id == id, cancellationToken);
        if (record is null || !record.IsAvailable(now))
            return true;

        try
        {
            record.AcquireLease(now, TimeSpan.FromMinutes(2));
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            context.ChangeTracker.Clear();
            return true;
        }

        object message;
        Type messageType;
        try
        {
            if (!MessageTypes.TryGetValue(record.MessageType, out var resolvedType))
                throw new InvalidOperationException($"Outbox message type '{record.MessageType}' is not registered.");

            messageType = resolvedType;
            message = JsonSerializer.Deserialize(record.Payload, messageType, JsonOptions)
                ?? throw new InvalidOperationException($"Outbox payload for '{record.MessageType}' was empty.");

            var publisher = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
            await publisher.Publish(message, messageType, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            record.RegisterFailure(DateTime.UtcNow, exception.Message);
            await context.SaveChangesAsync(cancellationToken);

            if (record.DeadLetteredAtUtc is not null)
            {
                logger.LogError(exception,
                    "Integration outbox message {MessageId} ({MessageType}) was dead-lettered after {Attempts} attempts.",
                    record.Id, record.MessageType, record.AttemptCount);
            }
            else
            {
                logger.LogWarning(exception,
                    "Integration outbox message {MessageId} ({MessageType}) failed on attempt {Attempt}; retry scheduled.",
                    record.Id, record.MessageType, record.AttemptCount);
            }

            return true;
        }

        // Broker acceptance and marking the row published are two separate systems. If
        // this database write fails, do not call RegisterFailure: the lease has already
        // been cleared in memory and RabbitMQ may already have accepted the message.
        // The persisted lease will expire and cause an at-least-once redelivery instead.
        try
        {
            record.MarkPublished(DateTime.UtcNow);
            await context.SaveChangesAsync(cancellationToken);
            logger.LogDebug("Published integration outbox message {MessageId} ({MessageType}).",
                record.Id, record.MessageType);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception,
                "Message {MessageId} ({MessageType}) was accepted by the broker, but its published marker could not be saved. The lease will expire and the message may be delivered again.",
                record.Id, record.MessageType);
        }

        return true;
    }
}
