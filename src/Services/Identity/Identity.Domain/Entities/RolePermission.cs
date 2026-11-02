using Shared.Common.Common;

namespace Identity.Domain.Entities;

public sealed class RolePermission : EntityBase
{
    private RolePermission() { }
    public RolePermission(Guid id, Guid roleId, Guid permissionId)
    {
        if (id == Guid.Empty) throw new ArgumentException("An entity id is required.", nameof(id));
        if (roleId == Guid.Empty) throw new ArgumentException("A role is required.", nameof(roleId));
        if (permissionId == Guid.Empty) throw new ArgumentException("A permission is required.", nameof(permissionId));
        Id = id;
        RoleId = roleId;
        PermissionId = permissionId;
    }
    public Guid RoleId { get; private set; }
    public Guid PermissionId { get; private set; }
    public Role Role { get; private set; } = null!;
    public Permission Permission { get; private set; } = null!;
}
