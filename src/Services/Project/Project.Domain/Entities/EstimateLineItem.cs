using Project.Domain.Enums;
using Shared.Common.Common;
namespace Project.Domain.Entities;
public sealed class EstimateLineItem : EntityBase
{
    private EstimateLineItem() { }
    public EstimateLineItem(Guid id, Guid estimateId, EstimateLineItemType itemType, string description,
        decimal quantity, string unitSymbol, decimal unitPrice, decimal? unitCost = null, Guid? catalogItemId = null)
    {
        if (id == Guid.Empty || estimateId == Guid.Empty) throw new ArgumentException("Line item and estimate ids are required.");
        Id = id; EstimateId = estimateId;
        Update(itemType, description, quantity, unitSymbol, unitPrice, unitCost, catalogItemId);
    }
    public Guid EstimateId { get; private set; }
    public EstimateLineItemType ItemType { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public string UnitSymbol { get; private set; } = string.Empty;
    public decimal UnitPrice { get; private set; }
    public decimal? UnitCost { get; private set; }
    public Guid? CatalogItemId { get; private set; }
    public decimal LineTotal => decimal.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);
    public decimal? LineCost => UnitCost is null ? null : decimal.Round(Quantity * UnitCost.Value, 2, MidpointRounding.AwayFromZero);
    public void Update(EstimateLineItemType itemType, string description, decimal quantity, string unitSymbol,
        decimal unitPrice, decimal? unitCost, Guid? catalogItemId)
    {
        if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Line description is required.", nameof(description));
        if (quantity <= 0 || quantity > 1_000_000_000m) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (string.IsNullOrWhiteSpace(unitSymbol) || unitSymbol.Trim().Length > 24) throw new ArgumentException("A measurement unit of at most 24 characters is required.", nameof(unitSymbol));
        if (unitPrice < 0 || unitPrice > 1_000_000_000_000m || (unitCost is not null && (unitCost < 0 || unitCost > 1_000_000_000_000m))) throw new ArgumentOutOfRangeException(nameof(unitPrice));
        if (catalogItemId == Guid.Empty) throw new ArgumentException("Optional catalog item id must be non-empty.");
        var roundedQuantity = decimal.Round(quantity, 3, MidpointRounding.AwayFromZero);
        if (roundedQuantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be at least 0.001.");
        ItemType = itemType; Description = description.Trim();
        Quantity = roundedQuantity; UnitSymbol = unitSymbol.Trim();
        UnitPrice = decimal.Round(unitPrice, 2, MidpointRounding.AwayFromZero);
        UnitCost = unitCost is null ? null : decimal.Round(unitCost.Value, 2, MidpointRounding.AwayFromZero);
        CatalogItemId = catalogItemId;
    }
}
