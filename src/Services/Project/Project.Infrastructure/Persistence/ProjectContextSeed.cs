using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Project.Domain.Entities;
using Project.Domain.Enums;

namespace Project.Infrastructure.Persistence;

public sealed class ProjectContextSeed
{
    // Stable seed identifiers so related entities always resolve consistently.
    private static readonly Guid ClientMapleId = Guid.Parse("a1000000-0000-0000-0000-000000000001");
    private static readonly Guid ClientOakId   = Guid.Parse("a1000000-0000-0000-0000-000000000002");
    private static readonly Guid PlotMapleId   = Guid.Parse("a2000000-0000-0000-0000-000000000001");
    private static readonly Guid PlotOakId     = Guid.Parse("a2000000-0000-0000-0000-000000000002");
    private static readonly Guid ProjectMapleId = Guid.Parse("a3000000-0000-0000-0000-000000000001");
    private static readonly Guid ProjectOakId   = Guid.Parse("a3000000-0000-0000-0000-000000000002");
    private static readonly Guid StageDesignId  = Guid.Parse("a4000000-0000-0000-0000-000000000001");
    private static readonly Guid StageBuildId   = Guid.Parse("a4000000-0000-0000-0000-000000000002");
    private static readonly Guid StagePlantId   = Guid.Parse("a4000000-0000-0000-0000-000000000003");
    private static readonly Guid EstimateMapleId = Guid.Parse("a5000000-0000-0000-0000-000000000001");
    private static readonly Guid LineItem1Id = Guid.Parse("a6000000-0000-0000-0000-000000000001");
    private static readonly Guid LineItem2Id = Guid.Parse("a6000000-0000-0000-0000-000000000002");
    private static readonly Guid LineItem3Id = Guid.Parse("a6000000-0000-0000-0000-000000000003");

    public static async Task SeedAsync(
        ProjectContext context,
        ILogger<ProjectContextSeed> logger,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

        if (await context.Clients.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Project seed skipped: clients already present.");
            return;
        }

        var mapleClient = new Client(ClientMapleId, "Maple Garden Estates", "contact@maplegarden.example", "+375 29 100 20 30", "Minsk, pr. Nezavisimosti 10");
        var oakClient = new Client(ClientOakId, "Oak Lawn Association", "info@oaklawn.example", "+375 33 200 40 50", "Brest, ul. Lenina 5");
        await context.Clients.AddRangeAsync([mapleClient, oakClient], cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        var maplePlot = new LandPlot(PlotMapleId, "Maple Plot 12A", "Minsk district, Borovlyany, plot 12A", 1200m, "Loam", "Flat terrain with southern exposure.");
        var oakPlot = new LandPlot(PlotOakId, "Oak Estate 7B", "Brest district, Baranovichi, plot 7B", 850m, "Sandy loam", "Slight slope toward the east boundary.");
        await context.LandPlots.AddRangeAsync([maplePlot, oakPlot], cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        var mapleProject = new LandscapeProject(ProjectMapleId, "LS-2026-001", "Maple Garden Landscape Design",
            ClientMapleId, PlotMapleId, "Complete landscape design and installation for a private estate including hardscape, planting, and irrigation.",
            new DateOnly(2026, 4, 1), new DateOnly(2026, 9, 30));
        mapleProject.ChangeStatus(ProjectStatus.InProgress, DateOnly.FromDateTime(DateTime.UtcNow));

        var oakProject = new LandscapeProject(ProjectOakId, "LS-2026-002", "Oak Lawn Renovation",
            ClientOakId, PlotOakId, "Renovation of an existing lawn area with new planting beds and a stone pathway.",
            new DateOnly(2026, 5, 15), new DateOnly(2026, 8, 15));
        oakProject.ChangeStatus(ProjectStatus.Planned, DateOnly.FromDateTime(DateTime.UtcNow));

        await context.Projects.AddRangeAsync([mapleProject, oakProject], cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        var designStage = new ProjectStage(StageDesignId, ProjectMapleId, "Design & Surveying", 1,
            "Site survey, soil testing, conceptual and detailed design.",
            new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30));
        designStage.ChangeStatus(StageStatus.Completed, new DateOnly(2026, 4, 28));

        var buildStage = new ProjectStage(StageBuildId, ProjectMapleId, "Hardscape Construction", 2,
            "Pathways, retaining walls, drainage, and irrigation installation.",
            new DateOnly(2026, 5, 1), new DateOnly(2026, 7, 15));
        buildStage.ChangeStatus(StageStatus.InProgress, new DateOnly(2026, 5, 5));

        var plantStage = new ProjectStage(StagePlantId, ProjectMapleId, "Planting & Finishing", 3,
            "Tree, shrub, and perennial planting; mulching; final cleanup.",
            new DateOnly(2026, 7, 16), new DateOnly(2026, 9, 30));
        plantStage.ChangeStatus(StageStatus.NotStarted, DateOnly.FromDateTime(DateTime.UtcNow));

        await context.Stages.AddRangeAsync([designStage, buildStage, plantStage], cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        var estimate = new Estimate(EstimateMapleId, ProjectMapleId, "V1",
            "Initial estimate covering design, hardscape, and planting phases.");

        var designLine = new EstimateLineItem(LineItem1Id, EstimateMapleId, EstimateLineItemType.Work,
            "Site survey and detailed landscape design", 40m, "ч", 45.00m, 30.00m);
        var hardscapeLine = new EstimateLineItem(LineItem2Id, EstimateMapleId, EstimateLineItemType.Material,
            "Natural stone for pathways and retaining walls", 55m, "м²", 28.50m, 19.00m);
        var plantingLine = new EstimateLineItem(LineItem3Id, EstimateMapleId, EstimateLineItemType.Plant,
            "Ornamental trees and shrubs (assorted species)", 25m, "шт.", 65.00m, 42.00m);

        estimate.AddLineItem(designLine);
        estimate.AddLineItem(hardscapeLine);
        estimate.AddLineItem(plantingLine);
        estimate.Submit(DateTime.UtcNow);
        estimate.Approve(DateTime.UtcNow);

        await context.Estimates.AddAsync(estimate, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Project seed completed: 2 clients, 2 plots, 2 projects, 3 stages, 1 approved estimate with 3 line items.");
    }
}
