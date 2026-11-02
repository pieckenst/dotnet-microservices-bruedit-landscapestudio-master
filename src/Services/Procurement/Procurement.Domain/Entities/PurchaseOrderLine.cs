using Procurement.Domain.Enums;
using Shared.Common.Common;

namespace Procurement.Domain.Entities;

public sealed class PurchaseOrderLine : EntityBase
{
    private PurchaseOrderLine() { }
    public PurchaseOrderLine(Guid id, Guid purchaseOrderId, ProcurementItemType itemType, Guid? catalogItemId,
        string description, string unitSymbol, decimal quantity, decimal unitPrice)
    {
        if (id == Guid.Empty || purchaseOrderId == Guid.Empty) throw new ArgumentException("Line and purchase-order ids are required.");
        Id = id; PurchaseOrderId = purchaseOrderId;
        Update(itemType, catalogItemId, description, unitSymbol, quantity, unitPrice);
    }
    public Guid PurchaseOrderId { get; private set; }
    public ProcurementItemType ItemType { get; private set; }
    public Guid? CatalogItemId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public string UnitSymbol { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public decimal ReceivedQuantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal LineTotal => decimal.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);
    public PurchaseOrder PurchaseOrder { get; private set; } = null!;

    public void Update(ProcurementItemType itemType, Guid? catalogItemId, string description, string unitSymbol,
        decimal quantity, decimal unitPrice)
    {
        if (!Enum.IsDefined(itemType)) throw new ArgumentOutOfRangeException(nameof(itemType));
        if (catalogItemId == Guid.Empty) throw new ArgumentException("Optional catalog item id must not be empty.", nameof(catalogItemId));
        if (string.IsNullOrWhiteSpace(description) || description.Trim().Length > 500) throw new ArgumentException("Line description is required and limited to 500 characters.", nameof(description));
        if (string.IsNullOrWhiteSpace(unitSymbol) || unitSymbol.Trim().Length > 24) throw new ArgumentException("Unit symbol is required and limited to 24 characters.", nameof(unitSymbol));
        if (quantity <= 0 || quantity > 1_000_000_000m) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (unitPrice < 0 || unitPrice > 1_000_000_000_000m) throw new ArgumentOutOfRangeException(nameof(unitPrice));
        if (ReceivedQuantity != 0 && (quantity < ReceivedQuantity || itemType != ItemType || catalogItemId != CatalogItemId))
            throw new InvalidOperationException("Received lines cannot reduce the ordered quantity or change item identity.");
        var roundedQuantity = decimal.Round(quantity, 3, MidpointRounding.AwayFromZero);
        if (roundedQuantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be at least 0.001.");
        ItemType = itemType; CatalogItemId = catalogItemId; Description = description.Trim();
        UnitSymbol = unitSymbol.Trim(); Quantity = roundedQuantity;
        UnitPrice = decimal.Round(unitPrice, 2, MidpointRounding.AwayFromZero);
    }

    public decimal Receive(decimal quantity)
    {
        if (quantity <= 0 || quantity > Quantity - ReceivedQuantity) throw new ArgumentOutOfRangeException(nameof(quantity), "Receipt quantity exceeds the remaining ordered quantity.");
        ReceivedQuantity = decimal.Round(ReceivedQuantity + quantity, 3, MidpointRounding.AwayFromZero);
        return quantity;
    }
}
