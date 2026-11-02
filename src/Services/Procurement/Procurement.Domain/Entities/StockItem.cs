using Shared.Common.Common;

namespace Procurement.Domain.Entities;

public sealed class StockItem : EntityBase
{
    private StockItem() { }
    public StockItem(Guid id, Guid? catalogItemId, string sku, string description, string unitSymbol,
        string storageLocation, decimal reorderLevel)
    {
        if (id == Guid.Empty) throw new ArgumentException("Stock item id is required.", nameof(id));
        if (catalogItemId == Guid.Empty) throw new ArgumentException("Optional catalog id must not be empty.", nameof(catalogItemId));
        Id = id; CatalogItemId = catalogItemId;
        Update(sku, description, unitSymbol, storageLocation, reorderLevel);
    }
    public Guid? CatalogItemId { get; private set; } // Catalog owns product metadata.
    public string Sku { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string UnitSymbol { get; private set; } = string.Empty;
    public string StorageLocation { get; private set; } = string.Empty;
    public decimal QuantityOnHand { get; private set; }
    public decimal ReorderLevel { get; private set; }
    public decimal AvailableQuantity => QuantityOnHand;
    public bool IsBelowReorderLevel => QuantityOnHand <= ReorderLevel;

    public void Update(string sku, string description, string unitSymbol, string storageLocation, decimal reorderLevel)
    {
        if (string.IsNullOrWhiteSpace(sku) || sku.Trim().Length > 48) throw new ArgumentException("SKU is required and limited to 48 characters.", nameof(sku));
        if (string.IsNullOrWhiteSpace(description) || description.Trim().Length > 200) throw new ArgumentException("Description is required and limited to 200 characters.", nameof(description));
        if (string.IsNullOrWhiteSpace(unitSymbol) || unitSymbol.Trim().Length > 24) throw new ArgumentException("Unit symbol is required and limited to 24 characters.", nameof(unitSymbol));
        if (string.IsNullOrWhiteSpace(storageLocation) || storageLocation.Trim().Length > 100) throw new ArgumentException("Storage location is required and limited to 100 characters.", nameof(storageLocation));
        if (reorderLevel < 0 || reorderLevel > 1_000_000_000m) throw new ArgumentOutOfRangeException(nameof(reorderLevel));
        Sku = sku.Trim().ToUpperInvariant(); Description = description.Trim(); UnitSymbol = unitSymbol.Trim();
        StorageLocation = storageLocation.Trim(); ReorderLevel = decimal.Round(reorderLevel, 3, MidpointRounding.AwayFromZero);
    }
    public void ApplyMovement(decimal signedQuantity)
    {
        if (signedQuantity == 0 || signedQuantity > 1_000_000_000m || signedQuantity < -1_000_000_000m)
            throw new ArgumentOutOfRangeException(nameof(signedQuantity));
        var next = decimal.Round(QuantityOnHand + signedQuantity, 3, MidpointRounding.AwayFromZero);
        if (next < 0) throw new InvalidOperationException("Stock quantity cannot become negative.");
        QuantityOnHand = next;
    }
}
