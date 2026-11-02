using Catalog.Application.Features.Plants;
using Catalog.Domain.Entities;
using Catalog.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Common.Exceptions;
using Shared.Common.Persistence;
using Xunit;

namespace Catalog.Tests;

public sealed class CatalogPersistenceTests
{
    [Fact]
    public async Task CreatePlantPersistsNormalizedValuesAndAuditTimestamp()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<CatalogContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new CatalogContext(options);
        await context.Database.EnsureCreatedAsync();
        await CatalogContextSeed.SeedAsync(context, NullLogger<CatalogContextSeed>.Instance);

        var unit = await context.MeasurementUnits.SingleAsync(item => item.Symbol == "шт.");
        var handler = new CreatePlantCommandHandler(
            new RepositoryBase<Plant>(context),
            new RepositoryBase<MeasurementUnit>(context));

        var created = await handler.Handle(
            new CreatePlantCommand("  Дуб  ", " Quercus robur ", "  Листопадное дерево  ",
                unit.Id, 10.129m),
            CancellationToken.None);

        Assert.Equal("Дуб", created.Name);
        Assert.Equal("Quercus robur", created.LatinName);
        Assert.Equal("Листопадное дерево", created.Description);
        Assert.Equal(10.13m, created.UnitPrice);
        Assert.NotEqual(default, created.CreatedDate);

        var stored = await context.Plants.AsNoTracking().SingleAsync();
        Assert.Equal(created.Id, stored.Id);
        Assert.Equal(unit.Id, stored.MeasurementUnitId);
    }

    [Fact]
    public async Task CreatePlantRejectsAnUnknownMeasurementUnit()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<CatalogContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new CatalogContext(options);
        await context.Database.EnsureCreatedAsync();

        var handler = new CreatePlantCommandHandler(
            new RepositoryBase<Plant>(context),
            new RepositoryBase<MeasurementUnit>(context));

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new CreatePlantCommand("Oak", "Quercus robur", string.Empty,
                Guid.NewGuid(), 12m),
            CancellationToken.None));
    }

    [Fact]
    public async Task PlantListUsesDatabasePaginationAndReportsTotalCount()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<CatalogContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new CatalogContext(options);
        await context.Database.EnsureCreatedAsync();
        await CatalogContextSeed.SeedAsync(context, NullLogger<CatalogContextSeed>.Instance);

        var plantHandler = new CreatePlantCommandHandler(
            new RepositoryBase<Plant>(context),
            new RepositoryBase<MeasurementUnit>(context));
        var unit = await context.MeasurementUnits.SingleAsync(item => item.Symbol == "шт.");

        foreach (var name in new[] { "Birch", "Oak", "Pine" })
        {
            await plantHandler.Handle(
                new CreatePlantCommand(name, null, string.Empty, unit.Id, 5m),
                CancellationToken.None);
        }

        var queryHandler = new GetPlantsQueryHandler(new RepositoryBase<Plant>(context));
        var page = await queryHandler.Handle(new GetPlantsQuery(Page: 1, PageSize: 2), CancellationToken.None);

        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, page.Items.Count);
        Assert.Equal(2, page.TotalPages);
        Assert.Equal(1, page.Page);
    }
}
