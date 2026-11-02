using Project.Domain.Enums;

namespace Project.Application.Contracts;

/// <summary>
/// Read-only boundary to the Catalog bounded context. Project stores only the Catalog id
/// and an estimate-time description/price/unit snapshot; it never owns Catalog data.
/// </summary>
public interface ICatalogItemLookup
{
    Task<CatalogItemReference?> FindAsync(
        Guid catalogItemId,
        EstimateLineItemType itemType,
        CancellationToken cancellationToken);
}

public sealed record CatalogItemReference(
    Guid Id,
    string Name,
    string Description,
    Guid MeasurementUnitId,
    string MeasurementUnitSymbol,
    decimal ListUnitPrice,
    bool IsActive,
    bool MeasurementUnitIsActive);
