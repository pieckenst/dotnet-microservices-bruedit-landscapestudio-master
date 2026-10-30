namespace Shared.Common.Outbox;

/// <summary>
/// Durable, at-least-once integration message. The record is written in the same
/// relational transaction as the business change that caused it.
/// </summary>
public sealed class IntegrationOutboxMessage
{
    public const int MaximumAttempts = 12;

    private IntegrationOutboxMessage() { }

    public IntegrationOutboxMessage(string messageType, string payload, DateTime createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(messageType))
            throw new ArgumentException("An integration message type is required.", nameof(messageType));
        if (string.IsNullOrWhiteSpace(payload))
            throw new ArgumentException("An integration message payload is required.", nameof(payload));

        Id = Guid.NewGuid();
        MessageType = messageType.Trim();
        Payload = payload;
        CreatedAtUtc = DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc);
        NextAttemptAtUtc = CreatedAtUtc;
        Version = 1;
    }

    public Guid Id { get; private set; }
    public string MessageType { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime NextAttemptAtUtc { get; private set; }
    public DateTime? LeaseExpiresAtUtc { get; private set; }
    public DateTime? PublishedAtUtc { get; private set; }
    public DateTime? DeadLetteredAtUtc { get; private set; }
    public int AttemptCount { get; private set; }
    public long Version { get; private set; }
    public string? LastError { get; private set; }

    public bool IsAvailable(DateTime nowUtc)
    {
        var now = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
        return PublishedAtUtc is null &&
               DeadLetteredAtUtc is null &&
               NextAttemptAtUtc <= now &&
               (LeaseExpiresAtUtc is null || LeaseExpiresAtUtc <= now);
    }

    public void AcquireLease(DateTime nowUtc, TimeSpan leaseDuration)
    {
        var now = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
        if (leaseDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        if (!IsAvailable(now))
            throw new InvalidOperationException("The outbox message is not currently available.");

        LeaseExpiresAtUtc = now.Add(leaseDuration);
        Version++;
    }

    public void MarkPublished(DateTime publishedAtUtc)
    {
        if (LeaseExpiresAtUtc is null)
            throw new InvalidOperationException("An outbox message must be leased before it can be completed.");

        PublishedAtUtc = DateTime.SpecifyKind(publishedAtUtc, DateTimeKind.Utc);
        LeaseExpiresAtUtc = null;
        LastError = null;
        Version++;
    }

    public void RegisterFailure(DateTime failedAtUtc, string error)
    {
        if (LeaseExpiresAtUtc is null)
            throw new InvalidOperationException("An outbox message must be leased before a failure can be recorded.");

        var now = DateTime.SpecifyKind(failedAtUtc, DateTimeKind.Utc);
        AttemptCount++;
        LeaseExpiresAtUtc = null;
        LastError = string.IsNullOrWhiteSpace(error)
            ? "Unspecified dispatch failure."
            : error.Trim()[..Math.Min(error.Trim().Length, 2000)];

        if (AttemptCount >= MaximumAttempts)
        {
            DeadLetteredAtUtc = now;
        }
        else
        {
            var retrySeconds = Math.Min(300, Math.Pow(2, Math.Min(AttemptCount, 8)) * 2);
            NextAttemptAtUtc = now.AddSeconds(retrySeconds);
        }

        Version++;
    }
}
