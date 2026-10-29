using Shared.Common.Common;

namespace Identity.Domain.Entities;

public sealed class Role : EntityBase
{
    private Role() { }
    public Role(Guid id, string name, string description, bool isSystemRole = false)
    {
        if (id == Guid.Empty) throw new ArgumentException("An entity id is required.", nameof(id));
        Id = id;
        Update(name, description, isSystemRole);
    }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public bool IsSystemRole { get; private set; }
    public ICollection<RolePermission> RolePermissions { get; private set; } = new List<RolePermission>();
    public void Update(string name, string description, bool isSystemRole)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Role name is required.", nameof(name));
        if (name.Trim().Length > 64) throw new ArgumentOutOfRangeException(nameof(name));
        Name = name.Trim();
        NormalizedName = Name.ToUpperInvariant();
        Description = description?.Trim() ?? string.Empty;
        IsSystemRole = isSystemRole;
    }
    public void SetActive(bool active)
    {
        if (IsSystemRole && !active) throw new InvalidOperationException("A built-in system role cannot be deactivated.");
        IsActive = active;
    }
}
