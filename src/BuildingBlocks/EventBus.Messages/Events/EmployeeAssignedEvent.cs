using EventBus.Messages.Common;

namespace EventBus.Messages.Events
{
    /// <summary>
    /// Published when an employee is assigned to a project or brigade.
    /// Consumed by Analytics Service for workforce tracking.
    /// </summary>
    public class EmployeeAssignedEvent : IntegrationBaseEvent
    {
        public Guid? EmployeeId { get; set; }
        public Guid ProjectId { get; set; }
        public Guid? BrigadeId { get; set; }
        public string EmployeeFullName { get; set; } = string.Empty;
        public string AssignmentRole { get; set; } = string.Empty;
        public bool IsResponsible { get; set; }
        public DateTime AssignedDate { get; set; }
    }
}
