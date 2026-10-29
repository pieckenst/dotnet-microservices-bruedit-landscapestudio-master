using EventBus.Messages.Common;

namespace EventBus.Messages.Events
{
    /// <summary>
    /// Published by the Identity Service when a new user registers.
    /// Consumed by the Account Service (to create a default profile) 
    /// and by the Analytics Service (audit trail).
    /// </summary>
    public class UserRegisteredEvent : IntegrationBaseEvent
    {
        public Guid UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public DateTime RegisteredAt { get; set; }
    }
}
