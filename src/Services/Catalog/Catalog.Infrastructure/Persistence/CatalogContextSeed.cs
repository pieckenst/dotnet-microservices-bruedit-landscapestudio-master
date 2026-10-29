using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Catalog.Infrastructure.Persistence;

public sealed class CatalogContextSeed
{
    private static MeasurementUnit[] CreateDefaultUnits() =>
    [
        new(Guid.Parse("b30e1c79-8e5d-40bb-8f8a-d07d2c2e0001"), "Piece", "шт."),
        new(Guid.Parse("b30e1c79-8e5d-40bb-8f8a-d07d2c2e0002"), "Square metre", "м²"),
        new(Guid.Parse("b30e1c79-8e5d-40bb-8f8a-d07d2c2e0003"), "Cubic metre", "м³"),
        new(Guid.Parse("b30e1c79-8e5d-40bb-8f8a-d07d2c2e0004"), "Linear metre", "пог. м"),
        new(Guid.Parse("b30e1c79-8e5d-40bb-8f8a-d07d2c2e0005"), "Hour", "ч")
    ];

    public static async Task SeedAsync(
        CatalogContext context,
        ILogger<CatalogContextSeed> logger,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

        if (!await context.MeasurementUnits.AnyAsync(cancellationToken))
        {
            var units = CreateDefaultUnits();
            await context.MeasurementUnits.AddRangeAsync(units, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded {Count} catalog measurement units.", units.Length);
        }
    }
}
