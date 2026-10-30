using Shared.Common.Common;
namespace Project.Domain.Entities;
public sealed class LandPlot : EntityBase
{
    private LandPlot() { }
    public LandPlot(Guid id, string name, string address, decimal areaSquareMetres, string? soilType = null, string? notes = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("An entity id is required.", nameof(id));
        Id = id;
        Update(name, address, areaSquareMetres, soilType, notes);
    }
    public string Name { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;
    public decimal AreaSquareMetres { get; private set; }
    public string? SoilType { get; private set; }
    public string? Notes { get; private set; }
    public bool IsArchived { get; private set; }
    public void Update(string name, string address, decimal areaSquareMetres, string? soilType, string? notes)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Plot name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(address)) throw new ArgumentException("Plot address is required.", nameof(address));
        if (areaSquareMetres <= 0 || areaSquareMetres > 100_000_000m) throw new ArgumentOutOfRangeException(nameof(areaSquareMetres));
        Name = name.Trim(); Address = address.Trim();
        var roundedArea = decimal.Round(areaSquareMetres, 2, MidpointRounding.AwayFromZero);
        if (roundedArea <= 0) throw new ArgumentOutOfRangeException(nameof(areaSquareMetres), "Area must be at least 0.01 square metres.");
        AreaSquareMetres = roundedArea;
        SoilType = string.IsNullOrWhiteSpace(soilType) ? null : soilType.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }
    public void Archive() => IsArchived = true;
    public void Restore() => IsArchived = false;
}
