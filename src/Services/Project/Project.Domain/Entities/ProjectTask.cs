using Project.Domain.Enums;
using Shared.Common.Common;
namespace Project.Domain.Entities;
/// <summary>Execution work item; employee and brigade ids refer to the Organization service.</summary>
public sealed class ProjectTask : EntityBase
{
    private ProjectTask() { }
    public ProjectTask(Guid id, Guid projectId, Guid stageId, string title, string description, DateOnly? dueDate = null,
        Guid? assignedEmployeeId = null, Guid? assignedBrigadeId = null)
    {
        if (id == Guid.Empty || projectId == Guid.Empty || stageId == Guid.Empty) throw new ArgumentException("Task, project, and stage ids are required.");
        Id = id; ProjectId = projectId; StageId = stageId;
        Update(title, description, dueDate, assignedEmployeeId, assignedBrigadeId); Status = ProjectTaskStatus.NotStarted;
    }
    public Guid ProjectId { get; private set; }
    public Guid StageId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public DateOnly? DueDate { get; private set; }
    public Guid? AssignedEmployeeId { get; private set; }
    public Guid? AssignedBrigadeId { get; private set; }
    public ProjectTaskStatus Status { get; private set; }
    public DateOnly? CompletedDate { get; private set; }
    public void Update(string title, string description, DateOnly? dueDate, Guid? assignedEmployeeId, Guid? assignedBrigadeId)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Task title is required.", nameof(title));
        if (title.Trim().Length > 200) throw new ArgumentOutOfRangeException(nameof(title));
        if (assignedEmployeeId == Guid.Empty || assignedBrigadeId == Guid.Empty) throw new ArgumentException("Optional assignment ids must be non-empty when supplied.");
        if (assignedEmployeeId is not null && assignedBrigadeId is not null) throw new ArgumentException("Assign either an employee or a brigade, not both.");
        Title = title.Trim(); Description = description?.Trim() ?? string.Empty; DueDate = dueDate;
        AssignedEmployeeId = assignedEmployeeId; AssignedBrigadeId = assignedBrigadeId;
    }
    public void ChangeStatus(ProjectTaskStatus status, DateOnly today)
    {
        if (Status == ProjectTaskStatus.Completed && status != ProjectTaskStatus.Completed) throw new InvalidOperationException("A completed task cannot be reopened.");
        Status = status; CompletedDate = status == ProjectTaskStatus.Completed ? today : null;
    }
}
