using Shared.Common.Common;

namespace Catalog.Domain.Entities;

/// <summary>A billable landscaping or design operation.</summary>
public sealed class WorkType : EntityBase
{
    private WorkType() { }

    public WorkType(
        Guid id,
        string name,
        string description,
        Guid measurementUnitId,
        decimal basePrice,
        bool isActive = true)
    {
        if (id == Guid.Empty) throw new ArgumentException("An entity id is required.", nameof(id));
        Id = id;
        Update(name, description, measurementUnitId, basePrice, isActive);
    }

    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public Guid MeasurementUnitId { get; private set; }
    public decimal BasePrice { get; private set; }
    public bool IsActive { get; private set; } = true;

    public void Update(
        string name,
        string description,
        Guid measurementUnitId,
        decimal basePrice,
        bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.", nameof(name));
        if (measurementUnitId == Guid.Empty) throw new ArgumentException("A measurement unit is required.", nameof(measurementUnitId));
        if (basePrice < 0) throw new ArgumentOutOfRangeException(nameof(basePrice), "Price cannot be negative.");

        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
        MeasurementUnitId = measurementUnitId;
        BasePrice = decimal.Round(basePrice, 2, MidpointRounding.AwayFromZero);
        IsActive = isActive;
    }
}
