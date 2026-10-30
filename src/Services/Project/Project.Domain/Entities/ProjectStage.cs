using Project.Domain.Enums;
using Shared.Common.Common;
namespace Project.Domain.Entities;
public sealed class ProjectStage : EntityBase
{
    private ProjectStage() { }
    public ProjectStage(Guid id, Guid projectId, string name, int sequence, string description,
        DateOnly? plannedStartDate = null, DateOnly? plannedEndDate = null)
    {
        if (id == Guid.Empty || projectId == Guid.Empty) throw new ArgumentException("Stage and project ids are required.");
        Id = id; ProjectId = projectId; Update(name, sequence, description, plannedStartDate, plannedEndDate);
        Status = StageStatus.NotStarted;
    }
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int Sequence { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public StageStatus Status { get; private set; }
    public DateOnly? PlannedStartDate { get; private set; }
    public DateOnly? PlannedEndDate { get; private set; }
    public DateOnly? ActualStartDate { get; private set; }
    public DateOnly? ActualEndDate { get; private set; }
    public ICollection<ProjectTask> Tasks { get; private set; } = new List<ProjectTask>();
    public void Update(string name, int sequence, string description, DateOnly? start, DateOnly? end)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Stage name is required.", nameof(name));
        if (sequence < 1) throw new ArgumentOutOfRangeException(nameof(sequence));
        if (start is not null && end is not null && end < start) throw new ArgumentException("Stage end date cannot be before start date.");
        Name = name.Trim(); Sequence = sequence; Description = description?.Trim() ?? string.Empty;
        PlannedStartDate = start; PlannedEndDate = end;
    }
    public void ChangeStatus(StageStatus status, DateOnly today)
    {
        if (Status == StageStatus.Completed && status != StageStatus.Completed)
            throw new InvalidOperationException("A completed stage cannot be reopened.");
        if (status == StageStatus.InProgress && ActualStartDate is null) ActualStartDate = today;
        if (status == StageStatus.Completed) { if (ActualStartDate is null) ActualStartDate = today; ActualEndDate = today; }
        Status = status;
    }
}
