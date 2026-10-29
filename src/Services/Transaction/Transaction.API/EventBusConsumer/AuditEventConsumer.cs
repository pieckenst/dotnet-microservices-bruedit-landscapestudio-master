using EventBus.Messages.Events;
using MassTransit;
using Transaction.API.Entities;
using Transaction.API.Repositories.Interfaces;

namespace Transaction.API.EventBusConsumer;

public sealed class AuditEventConsumer(
    ILogger<AuditEventConsumer> logger,
    IAuditEventRepository repository) : IConsumer<AuditEvent>
{
    public async Task Consume(ConsumeContext<AuditEvent> context)
    {
        var message = context.Message;
        if (message.Id == Guid.Empty ||
            string.IsNullOrWhiteSpace(message.SourceService) ||
            string.IsNullOrWhiteSpace(message.Action) ||
            string.IsNullOrWhiteSpace(message.EntityType))
        {
            logger.LogWarning("Ignoring malformed audit event {MessageId}", message.Id);
            return;
        }

        var record = new AuditRecord
        {
            Id = message.Id,
            OccurredAtUtc = message.CreationDate.Kind == DateTimeKind.Utc
                ? message.CreationDate
                : DateTime.SpecifyKind(message.CreationDate, DateTimeKind.Utc),
            SourceService = message.SourceService.Trim(),
            Action = message.Action.Trim(),
            EntityType = message.EntityType.Trim(),
            EntityId = Normalize(message.EntityId),
            ActorId = Normalize(message.ActorId),
            CorrelationId = Normalize(message.CorrelationId) ?? context.CorrelationId?.ToString(),
            Metadata = message.Metadata is null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(message.Metadata
                    .Where(item => !string.IsNullOrWhiteSpace(item.Key) && item.Value is not null)
                    .Take(30)
                    .ToDictionary(item => item.Key.Trim(), item => Limit(item.Value, 500)), StringComparer.Ordinal)
        };

        await repository.AddAsync(record, context.CancellationToken);
        logger.LogDebug("Stored audit event {EventId} from {SourceService}", record.Id, record.SourceService);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : Limit(value.Trim(), 128);

    private static string Limit(string value, int maximum) =>
        value.Length <= maximum ? value : value[..maximum];
}
