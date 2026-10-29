using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Project.Domain.Entities;
using Project.Infrastructure.Persistence;
using Xunit;

namespace Project.Tests;

public sealed class ProjectPersistenceTests
{
    [Fact]
    public async Task ProjectContextPersistsClientPlotAndTheirProjectReferences()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ProjectContext>().UseSqlite(connection).Options;
        await using var context = new ProjectContext(options);
        await context.Database.EnsureCreatedAsync();

        var client = new Client(Guid.NewGuid(), "North Garden Ltd", "office@example.test", "+375290000000", "Minsk");
        var plot = new LandPlot(Guid.NewGuid(), "Demo parcel", "Minsk, Belarus", 525.5m);
        context.Clients.Add(client);
        context.LandPlots.Add(plot);
        await context.SaveChangesAsync();

        var project = new LandscapeProject(Guid.NewGuid(), "pr-001", "Demo garden", client.Id, plot.Id,
            "Integration test project");
        context.Projects.Add(project);
        await context.SaveChangesAsync();

        var saved = await context.Projects.AsNoTracking().SingleAsync();
        Assert.Equal("PR-001", saved.ProjectNumber);
        Assert.Equal(client.Id, saved.ClientId);
        Assert.Equal(plot.Id, saved.LandPlotId);
        Assert.NotEqual(default, saved.CreatedDate);
    }
}
