using EventBus.Messages.Common;

namespace EventBus.Messages.Events
{
    /// <summary>
    /// Published by the Account Service when a user profile is updated.
    /// Consumed by the Analytics Service for audit trail.
    /// </summary>
    public class ProfileUpdatedEvent : IntegrationBaseEvent
    {
        public Guid UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string UpdatedFields { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
    }
}
