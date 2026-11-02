using System.Text.Json;

namespace LandscapeStudio.Web.Models;

public sealed class WorkAssignmentListViewModel
{
    public IReadOnlyList<WorkAssignmentRow> Items { get; set; } = Array.Empty<WorkAssignmentRow>();
    public string? Error { get; set; }
}

public sealed record WorkAssignmentRow(
    Guid Id,
    string Project,
    string AssignedTo,
    string Title,
    DateOnly StartDate,
    DateOnly? EndDate,
    string Status,
    string Notes);

public sealed class WorkAssignmentFormViewModel
{
    public Guid ProjectId { get; set; }
    public Guid? EmployeeId { get; set; }
    public Guid? BrigadeId { get; set; }
    public Guid? ProjectTaskId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public DateOnly? EndDate { get; set; }
    public string Notes { get; set; } = string.Empty;
    public IReadOnlyList<AssignmentProjectOption> Projects { get; set; } = Array.Empty<AssignmentProjectOption>();
    public IReadOnlyList<AssignmentEmployeeOption> Employees { get; set; } = Array.Empty<AssignmentEmployeeOption>();
    public IReadOnlyList<AssignmentBrigadeOption> Brigades { get; set; } = Array.Empty<AssignmentBrigadeOption>();
    public string? LookupError { get; set; }
}

public sealed record AssignmentProjectOption(Guid Id, string ProjectNumber, string Name);
public sealed record AssignmentEmployeeOption(Guid Id, string EmployeeNumber, string FullName);
public sealed record AssignmentBrigadeOption(Guid Id, string Name);
