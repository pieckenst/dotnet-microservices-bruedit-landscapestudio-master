using Shared.Common.Common;

namespace Catalog.Domain.Entities;

/// <summary>A plant offered by the landscaping studio's catalog.</summary>
public sealed class Plant : EntityBase
{
    private Plant() { }

    public Plant(
        Guid id,
        string name,
        string? latinName,
        string description,
        Guid measurementUnitId,
        decimal unitPrice,
        bool isActive = true)
    {
        if (id == Guid.Empty) throw new ArgumentException("An entity id is required.", nameof(id));
        Id = id;
        Update(name, latinName, description, measurementUnitId, unitPrice, isActive);
    }

    public string Name { get; private set; } = string.Empty;
    public string? LatinName { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public Guid MeasurementUnitId { get; private set; }
    public decimal UnitPrice { get; private set; }
    public bool IsActive { get; private set; } = true;

    public void Update(
        string name,
        string? latinName,
        string description,
        Guid measurementUnitId,
        decimal unitPrice,
        bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.", nameof(name));
        if (measurementUnitId == Guid.Empty) throw new ArgumentException("A measurement unit is required.", nameof(measurementUnitId));
        if (unitPrice < 0) throw new ArgumentOutOfRangeException(nameof(unitPrice), "Price cannot be negative.");

        Name = name.Trim();
        LatinName = string.IsNullOrWhiteSpace(latinName) ? null : latinName.Trim();
        Description = description?.Trim() ?? string.Empty;
        MeasurementUnitId = measurementUnitId;
        UnitPrice = decimal.Round(unitPrice, 2, MidpointRounding.AwayFromZero);
        IsActive = isActive;
    }
}
