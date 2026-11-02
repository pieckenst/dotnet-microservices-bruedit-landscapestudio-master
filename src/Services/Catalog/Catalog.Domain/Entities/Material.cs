using Shared.Common.Common;

namespace Catalog.Domain.Entities;

/// <summary>A consumable or construction material used by projects.</summary>
public sealed class Material : EntityBase
{
    private Material() { }

    public Material(
        Guid id,
        string name,
        string sku,
        string description,
        Guid measurementUnitId,
        decimal unitPrice,
        bool isActive = true)
    {
        if (id == Guid.Empty) throw new ArgumentException("An entity id is required.", nameof(id));
        Id = id;
        Update(name, sku, description, measurementUnitId, unitPrice, isActive);
    }

    public string Name { get; private set; } = string.Empty;
    public string Sku { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public Guid MeasurementUnitId { get; private set; }
    public decimal UnitPrice { get; private set; }
    public bool IsActive { get; private set; } = true;

    public void Update(
        string name,
        string sku,
        string description,
        Guid measurementUnitId,
        decimal unitPrice,
        bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(sku)) throw new ArgumentException("SKU is required.", nameof(sku));
        if (measurementUnitId == Guid.Empty) throw new ArgumentException("A measurement unit is required.", nameof(measurementUnitId));
        if (unitPrice < 0) throw new ArgumentOutOfRangeException(nameof(unitPrice), "Price cannot be negative.");

        Name = name.Trim();
        Sku = sku.Trim().ToUpperInvariant();
        Description = description?.Trim() ?? string.Empty;
        MeasurementUnitId = measurementUnitId;
        UnitPrice = decimal.Round(unitPrice, 2, MidpointRounding.AwayFromZero);
        IsActive = isActive;
    }
}
