using Project.Domain.Enums;
using Shared.Common.Common;
namespace Project.Domain.Entities;
/// <summary>Coordinates delivery of a landscape-design job for a client and land plot.</summary>
public sealed class LandscapeProject : EntityBase
{
    private LandscapeProject() { }
    public LandscapeProject(Guid id, string projectNumber, string name, Guid clientId, Guid landPlotId, string description,
        DateOnly? plannedStartDate = null, DateOnly? plannedEndDate = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("An entity id is required.", nameof(id));
        Id = id;
        UpdateDetails(projectNumber, name, clientId, landPlotId, description, plannedStartDate, plannedEndDate);
        Status = ProjectStatus.Draft;
    }
    public string ProjectNumber { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public Guid ClientId { get; private set; }
    public Guid LandPlotId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public ProjectStatus Status { get; private set; }
    public DateOnly? PlannedStartDate { get; private set; }
    public DateOnly? PlannedEndDate { get; private set; }
    public DateOnly? ActualStartDate { get; private set; }
    public DateOnly? ActualEndDate { get; private set; }
    public ICollection<ProjectStage> Stages { get; private set; } = new List<ProjectStage>();
    public void UpdateDetails(string projectNumber, string name, Guid clientId, Guid landPlotId, string description,
        DateOnly? plannedStartDate, DateOnly? plannedEndDate)
    {
        if (string.IsNullOrWhiteSpace(projectNumber)) throw new ArgumentException("Project number is required.", nameof(projectNumber));
        if (projectNumber.Trim().Length > 40) throw new ArgumentOutOfRangeException(nameof(projectNumber));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Project name is required.", nameof(name));
        if (name.Trim().Length > 200) throw new ArgumentOutOfRangeException(nameof(name));
        if (clientId == Guid.Empty || landPlotId == Guid.Empty) throw new ArgumentException("Client and land plot are required.");
        if (plannedStartDate is not null && plannedEndDate is not null && plannedEndDate < plannedStartDate)
            throw new ArgumentException("Planned end date cannot be before planned start date.");
        ProjectNumber = projectNumber.Trim().ToUpperInvariant(); Name = name.Trim();
        ClientId = clientId; LandPlotId = landPlotId; Description = description?.Trim() ?? string.Empty;
        PlannedStartDate = plannedStartDate; PlannedEndDate = plannedEndDate;
    }
    public void ChangeStatus(ProjectStatus status, DateOnly today)
    {
        if ((Status is ProjectStatus.Completed or ProjectStatus.Cancelled) && status != Status)
            throw new InvalidOperationException("A completed or cancelled project is terminal.");
        if (status == ProjectStatus.InProgress && ActualStartDate is null) ActualStartDate = today;
        if (status is ProjectStatus.Completed or ProjectStatus.Cancelled)
        {
            if (status == ProjectStatus.Completed && ActualStartDate is null) ActualStartDate = today;
            ActualEndDate = today;
        }
        Status = status;
    }
}
