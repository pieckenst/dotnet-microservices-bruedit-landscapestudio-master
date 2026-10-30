using Shared.Common.Outbox;
using Xunit;

namespace Project.Tests;

public sealed class IntegrationOutboxMessageLifecycleTests
{
    [Fact]
    public void PublishedMessageCannotBeClaimedAgain()
    {
        var created = DateTime.UtcNow;
        var message = new IntegrationOutboxMessage(
            "EventBus.Messages.Events.AuditEvent", "{}", created);

        Assert.True(message.IsAvailable(created));
        message.AcquireLease(created, TimeSpan.FromMinutes(2));
        Assert.False(message.IsAvailable(created.AddSeconds(30)));
        Assert.True(message.IsAvailable(created.AddMinutes(3)));

        message.MarkPublished(created.AddSeconds(45));

        Assert.NotNull(message.PublishedAtUtc);
        Assert.Null(message.LeaseExpiresAtUtc);
        Assert.False(message.IsAvailable(created.AddMinutes(4)));
    }

    [Fact]
    public void RepeatedDispatchFailuresBackOffAndEventuallyDeadLetter()
    {
        var now = DateTime.UtcNow;
        var message = new IntegrationOutboxMessage(
            "EventBus.Messages.Events.AuditEvent", "{}", now);

        for (var attempt = 1; attempt <= IntegrationOutboxMessage.MaximumAttempts; attempt++)
        {
            Assert.True(message.IsAvailable(now));
            message.AcquireLease(now, TimeSpan.FromMinutes(2));
            message.RegisterFailure(now, "RabbitMQ unavailable");

            Assert.Equal(attempt, message.AttemptCount);
            Assert.Null(message.LeaseExpiresAtUtc);

            if (attempt < IntegrationOutboxMessage.MaximumAttempts)
            {
                Assert.Null(message.DeadLetteredAtUtc);
                Assert.True(message.NextAttemptAtUtc > now);
                now = message.NextAttemptAtUtc;
            }
        }

        Assert.NotNull(message.DeadLetteredAtUtc);
        Assert.Equal("RabbitMQ unavailable", message.LastError);
        Assert.False(message.IsAvailable(now.AddDays(1)));
    }
}
