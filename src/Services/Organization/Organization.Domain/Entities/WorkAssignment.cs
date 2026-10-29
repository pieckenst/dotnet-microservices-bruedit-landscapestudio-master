using Organization.Domain.Enums;
using Shared.Common.Common;

namespace Organization.Domain.Entities;

/// <summary>References Project and ProjectTask by identifiers; no cross-service foreign keys.</summary>
public sealed class WorkAssignment : EntityBase
{
    private WorkAssignment() { }
    public WorkAssignment(Guid id, Guid? employeeId, Guid? brigadeId, Guid projectId, Guid? projectTaskId,
        string title, DateOnly startDate, DateOnly? endDate, string notes = "")
    {
        if (id == Guid.Empty || projectId == Guid.Empty) throw new ArgumentException("Assignment and project ids are required.");
        Id = id; ProjectId = projectId; ProjectTaskId = projectTaskId;
        Update(employeeId, brigadeId, title, startDate, endDate, notes);
        Status = AssignmentStatus.Planned;
    }
    public Guid? EmployeeId { get; private set; }
    public Guid? BrigadeId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid? ProjectTaskId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public DateOnly StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public AssignmentStatus Status { get; private set; }
    public string Notes { get; private set; } = string.Empty;
    public void Update(Guid? employeeId, Guid? brigadeId, string title, DateOnly startDate, DateOnly? endDate, string notes)
    {
        if ((employeeId is null) == (brigadeId is null))
            throw new ArgumentException("Assignment must belong to exactly one employee or one brigade.");
        if (employeeId == Guid.Empty || brigadeId == Guid.Empty || ProjectTaskId == Guid.Empty)
            throw new ArgumentException("Optional assignment ids must not be empty.");
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 160) throw new ArgumentException("Assignment title is required and limited to 160 characters.", nameof(title));
        if (endDate is not null && endDate < startDate) throw new ArgumentException("Assignment end date cannot precede its start date.");
        if (notes?.Length > 2000) throw new ArgumentOutOfRangeException(nameof(notes));
        EmployeeId = employeeId; BrigadeId = brigadeId; Title = title.Trim();
        StartDate = startDate; EndDate = endDate; Notes = notes?.Trim() ?? string.Empty;
    }
    public void ChangeStatus(AssignmentStatus status)
    {
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        if ((Status is AssignmentStatus.Completed or AssignmentStatus.Cancelled) && status != Status)
            throw new InvalidOperationException("A completed or cancelled assignment is terminal.");
        Status = status;
    }
}
