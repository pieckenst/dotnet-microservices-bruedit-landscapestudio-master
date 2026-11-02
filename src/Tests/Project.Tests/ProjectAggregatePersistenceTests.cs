using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Project.Domain.Entities;
using Project.Domain.Enums;
using Project.Infrastructure.Persistence;
using Project.Infrastructure.Repositories;
using Xunit;

namespace Project.Tests;

public sealed class ProjectAggregatePersistenceTests
{
    [Fact]
    public async Task TaskCannotReferenceStageBelongingToAnotherProject()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ProjectContext>().UseSqlite(connection).Options;
        await using var context = new ProjectContext(options);
        await context.Database.EnsureCreatedAsync();

        var client = new Client(Guid.NewGuid(), "Client", null, null, null);
        var plot1 = new LandPlot(Guid.NewGuid(), "Plot 1", "Address 1", 100m);
        var plot2 = new LandPlot(Guid.NewGuid(), "Plot 2", "Address 2", 200m);
        context.AddRange(client, plot1, plot2);
        await context.SaveChangesAsync();

        var project1 = new LandscapeProject(Guid.NewGuid(), "P-001", "Project One", client.Id, plot1.Id, "");
        var project2 = new LandscapeProject(Guid.NewGuid(), "P-002", "Project Two", client.Id, plot2.Id, "");
        context.Projects.AddRange(project1, project2);
        await context.SaveChangesAsync();

        var stage = new ProjectStage(Guid.NewGuid(), project1.Id, "Ground preparation", 1, "");
        context.Stages.Add(stage);
        await context.SaveChangesAsync();

        var invalidTask = new ProjectTask(Guid.NewGuid(), project2.Id, stage.Id, "Dig", "",
            assignedEmployeeId: null, assignedBrigadeId: null);
        context.Tasks.Add(invalidTask);
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task EstimateRepositoryPersistsLineItemsAndStateTransitions()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ProjectContext>().UseSqlite(connection).Options;
        await using var context = new ProjectContext(options);
        await context.Database.EnsureCreatedAsync();

        var client = new Client(Guid.NewGuid(), "Client", null, null, null);
        var plot = new LandPlot(Guid.NewGuid(), "Garden", "Minsk", 600m);
        context.AddRange(client, plot);
        await context.SaveChangesAsync();

        var project = new LandscapeProject(Guid.NewGuid(), "EST-001", "Garden design", client.Id, plot.Id, "");
        context.Projects.Add(project);
        await context.SaveChangesAsync();

        var repository = new EstimateRepository(context);
        var estimate = new Estimate(Guid.NewGuid(), project.Id, "v1");
        await repository.AddAsync(estimate, CancellationToken.None);
        estimate = (await repository.GetByIdWithItemsAsync(estimate.Id, tracking: true, CancellationToken.None))!;

        var lineItem = new EstimateLineItem(Guid.NewGuid(), estimate.Id, EstimateLineItemType.Plant,
            "Birch tree", 2m, "pcs", 35m, 20m);
        await repository.AddLineItemAsync(estimate, lineItem, CancellationToken.None);
        estimate = (await repository.GetByIdWithItemsAsync(estimate.Id, tracking: true, CancellationToken.None))!;
        Assert.Single(estimate.LineItems);
        Assert.Equal(70m, estimate.EstimatedTotal);
        Assert.Equal(40m, estimate.EstimatedCost);

        estimate.Submit(new DateTime(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc));
        await repository.SaveAsync(estimate, CancellationToken.None);
        estimate.Approve(new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc));
        await repository.SaveAsync(estimate, CancellationToken.None);

        var saved = await repository.GetByIdWithItemsAsync(estimate.Id, tracking: false, CancellationToken.None);
        Assert.NotNull(saved);
        Assert.Equal(EstimateStatus.Approved, saved!.Status);
        Assert.Equal(70m, saved.EstimatedTotal);
        Assert.Equal(40m, saved.EstimatedCost);
        Assert.Single(saved.LineItems);
    }
}
