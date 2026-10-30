using Shared.Common.Outbox;
using Xunit;

namespace Project.Tests;

public sealed class IntegrationOutboxMessageTests
{
    [Fact]
    public void FailedDeliveryUsesBoundedBackoffAndEventuallyDeadLetters()
    {
        var now = new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
        var message = new IntegrationOutboxMessage("EventBus.Messages.Events.AuditEvent", "{}", now);

        for (var attempt = 1; attempt <= IntegrationOutboxMessage.MaximumAttempts; attempt++)
        {
            var readyAt = message.NextAttemptAtUtc;
            Assert.True(message.IsAvailable(readyAt));
            message.AcquireLease(readyAt, TimeSpan.FromMinutes(2));
            Assert.False(message.IsAvailable(readyAt));
            message.RegisterFailure(readyAt, "broker unavailable");
            Assert.Equal(attempt, message.AttemptCount);
        }

        Assert.NotNull(message.DeadLetteredAtUtc);
        Assert.False(message.IsAvailable(DateTime.UtcNow));
        Assert.Null(message.LeaseExpiresAtUtc);
        Assert.Equal("broker unavailable", message.LastError);
    }

    [Fact]
    public void PublishedMessageCannotBeDispatchedAgain()
    {
        var now = new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
        var message = new IntegrationOutboxMessage("EventBus.Messages.Events.AuditEvent", "{}", now);

        message.AcquireLease(now, TimeSpan.FromMinutes(2));
        message.MarkPublished(now.AddSeconds(1));

        Assert.NotNull(message.PublishedAtUtc);
        Assert.False(message.IsAvailable(now.AddDays(1)));
    }

    [Fact]
    public void ALeaseExpiresAndMakesAnUndeliveredMessageAvailableAgain()
    {
        var now = new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
        var message = new IntegrationOutboxMessage("EventBus.Messages.Events.AuditEvent", "{}", now);

        message.AcquireLease(now, TimeSpan.FromMinutes(2));

        Assert.False(message.IsAvailable(now.AddMinutes(1)));
        Assert.True(message.IsAvailable(now.AddMinutes(3)));
    }
}
