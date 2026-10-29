using EventBus.Messages.Common;
using EventBus.Messages.Enums;

namespace EventBus.Messages.Events
{
    /// <summary>
    /// Published when a project stage is marked as completed.
    /// Consumed by Analytics Service for tracking project progress.
    /// </summary>
    public class ProjectStageCompletedEvent : IntegrationBaseEvent
    {
        public Guid ProjectId { get; set; }
        public Guid StageId { get; set; }
        public string StageName { get; set; } = string.Empty;
        public int CompletedTaskCount { get; set; }
        public int TotalTaskCount { get; set; }
        public DateTime CompletedDate { get; set; }
        public ProjectStatus ProjectStatus { get; set; }
    }
}
