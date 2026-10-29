using Shared.Common.Common;

namespace Catalog.Domain.Entities;

/// <summary>A unit used to quote plant, material, and work quantities.</summary>
public sealed class MeasurementUnit : EntityBase
{
    private MeasurementUnit() { }

    public MeasurementUnit(Guid id, string name, string symbol, bool isActive = true)
    {
        if (id == Guid.Empty) throw new ArgumentException("An entity id is required.", nameof(id));
        Id = id;
        Update(name, symbol, isActive);
    }

    public string Name { get; private set; } = string.Empty;
    public string Symbol { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    public void Update(string name, string symbol, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(symbol)) throw new ArgumentException("Symbol is required.", nameof(symbol));
        Name = name.Trim();
        Symbol = symbol.Trim();
        IsActive = isActive;
    }
}
