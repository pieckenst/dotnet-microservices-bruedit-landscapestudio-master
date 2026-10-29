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

        await context.SaveChangesAsync(message.CancellationToken);
        logger.LogInformation("Ensured default Account profile for registered identity {UserId}", evt.UserId);
    }
}
