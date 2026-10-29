using Project.Domain.Entities;
using Project.Domain.Enums;
using Xunit;

namespace Project.Tests;

public sealed class ProjectDomainTests
{
    [Fact]
    public void LandPlotRejectsNonPositiveAreaAndNormalizesValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LandPlot(Guid.NewGuid(), "Garden", "Minsk", 0));

        var plot = new LandPlot(Guid.NewGuid(), "  Garden  ", " Minsk, Belarus ", 125.678m, "  Loam ", "  south-facing ");
        Assert.Equal("Garden", plot.Name);
        Assert.Equal("Minsk, Belarus", plot.Address);
        Assert.Equal(125.68m, plot.AreaSquareMetres);
        Assert.Equal("Loam", plot.SoilType);
        Assert.Equal("south-facing", plot.Notes);
    }

    [Fact]
    public void ProjectCannotHaveAnInvalidDateRangeOrLeaveTerminalStatus()
    {
        Assert.Throws<ArgumentException>(() => new LandscapeProject(
            Guid.NewGuid(), "LS-001", "New garden", Guid.NewGuid(), Guid.NewGuid(), string.Empty,
            new DateOnly(2026, 5, 2), new DateOnly(2026, 5, 1)));

        var project = new LandscapeProject(
            Guid.NewGuid(), "ls-002", "Courtyard redesign", Guid.NewGuid(), Guid.NewGuid(), "Redesign courtyard");
        project.ChangeStatus(ProjectStatus.InProgress, new DateOnly(2026, 5, 10));
        project.ChangeStatus(ProjectStatus.Completed, new DateOnly(2026, 6, 1));

        Assert.Equal("LS-002", project.ProjectNumber);
        Assert.Equal(new DateOnly(2026, 5, 10), project.ActualStartDate);
        Assert.Equal(new DateOnly(2026, 6, 1), project.ActualEndDate);
        Assert.Throws<InvalidOperationException>(() =>
            project.ChangeStatus(ProjectStatus.InProgress, new DateOnly(2026, 6, 2)));
    }

    [Fact]
    public void EstimateCalculatesRoundedTotalsAndRequiresLinesBeforeSubmission()
    {
        var estimate = new Estimate(Guid.NewGuid(), Guid.NewGuid(), "v1");
        Assert.Throws<InvalidOperationException>(() => estimate.Submit(DateTime.UtcNow));

        estimate.AddLineItem(new EstimateLineItem(
            Guid.NewGuid(), estimate.Id, EstimateLineItemType.Material, "Decorative stone",
            2.5m, "kg", 25.129m, 10m));
        estimate.AddLineItem(new EstimateLineItem(
            Guid.NewGuid(), estimate.Id, EstimateLineItemType.Work, "Installation",
            3m, "h", 12.50m, 8m));

        Assert.Equal(100.33m, estimate.EstimatedTotal);
        Assert.Equal(49m, estimate.EstimatedCost);

        estimate.Submit(new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc));
        estimate.Approve(new DateTime(2026, 5, 2, 12, 0, 0, DateTimeKind.Utc));
        Assert.Equal(EstimateStatus.Approved, estimate.Status);
        Assert.Throws<InvalidOperationException>(() => estimate.AddLineItem(
            new EstimateLineItem(Guid.NewGuid(), estimate.Id, EstimateLineItemType.Other,
                "Extra", 1m, "unit", 1m)));
    }
}
