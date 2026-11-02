using Account.Domain.Entities;
using Account.Infrastructure.Persistence;
using EventBus.Messages.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Account.API.EventBus;

public sealed class UserRegisteredConsumer(
    AccountContext context,
    ILogger<UserRegisteredConsumer> logger) : IConsumer<UserRegisteredEvent>
{
    public async Task Consume(ConsumeContext<UserRegisteredEvent> message)
    {
        var evt = message.Message;
        if (evt.UserId == Guid.Empty || string.IsNullOrWhiteSpace(evt.Username))
        {
            logger.LogWarning("Ignoring malformed user-registered event {EventId}", evt.Id);
            return;
        }

        var profile = await context.AccountProfiles.FindAsync([evt.UserId], message.CancellationToken);
        if (profile is null)
        {
            profile = new AccountProfile(evt.UserId, evt.Username);
            await context.AccountProfiles.AddAsync(profile, message.CancellationToken);
        }

        var preferences = await context.AccountPreferences.FindAsync([evt.UserId], message.CancellationToken);
        if (preferences is null)
            await context.AccountPreferences.AddAsync(new AccountPreference(evt.UserId), message.CancellationToken);

        try
        {
            await context.SaveChangesAsync(message.CancellationToken);
        }
        catch (DbUpdateException exception)
        {
            // UserId is the primary key for both profile and preferences. Two deliveries
            // can race after both observed a missing row; if the competing transaction
            // committed both records, this delivery is already satisfied and is safe to ack.
            context.ChangeTracker.Clear();
            var profileExists = await context.AccountProfiles.AsNoTracking()
                .AnyAsync(item => item.UserId == evt.UserId, message.CancellationToken);
            var preferencesExist = await context.AccountPreferences.AsNoTracking()
                .AnyAsync(item => item.UserId == evt.UserId, message.CancellationToken);

            if (!profileExists || !preferencesExist)
            {
                logger.LogError(exception,
                    "User registration event {EventId} could not ensure Account defaults for {UserId}.",
                    evt.Id, evt.UserId);
                throw;
            }

            logger.LogInformation(
                "Acknowledging duplicate user registration event {EventId}; Account defaults already exist for {UserId}.",
                evt.Id, evt.UserId);
            return;
        }

        logger.LogInformation("Ensured default Account profile for registered identity {UserId}", evt.UserId);
    }
}
