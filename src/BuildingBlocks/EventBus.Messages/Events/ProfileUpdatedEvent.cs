namespace EventBus.Messages.Events;

/// <summary>
/// A privacy-minimizing profile change notification. Never place profile field values in the event.
/// </summary>
public sealed class ProfileUpdatedEvent : IntegrationBaseEvent
{
    public Guid UserId { get; set; }
    public string UpdatedFields { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}
