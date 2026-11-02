using Shared.Common.Common;

namespace Identity.Domain.Entities;

public sealed class Permission : EntityBase
{
    private Permission() { }
    public Permission(Guid id, string name, string description)
    {
        if (id == Guid.Empty) throw new ArgumentException("An entity id is required.", nameof(id));
        Id = id;
        Update(name, description);
    }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public void Update(string name, string description)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Permission name is required.", nameof(name));
        if (name.Trim().Length > 128) throw new ArgumentOutOfRangeException(nameof(name));
        Name = name.Trim();
        NormalizedName = Name.ToUpperInvariant();
        Description = description?.Trim() ?? string.Empty;
    }
    public void SetActive(bool active) => IsActive = active;
}
