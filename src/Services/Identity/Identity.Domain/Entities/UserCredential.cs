using Shared.Common.Common;

namespace Identity.Domain.Entities;

/// <summary>Authentication identity only; profile and preferences belong to Account.</summary>
public sealed class UserCredential : EntityBase
{
    private UserCredential() { }
    public UserCredential(Guid id, string userName, Guid roleId, string passwordHash, bool isActive = true)
    {
        if (id == Guid.Empty) throw new ArgumentException("An entity id is required.", nameof(id));
        Id = id;
        SetUserName(userName);
        ChangeRole(roleId);
        SetPasswordHash(passwordHash);
        IsActive = isActive;
    }
    public string UserName { get; private set; } = string.Empty;
    public string NormalizedUserName { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public Guid RoleId { get; private set; }
    public Role Role { get; private set; } = null!;
    public bool IsActive { get; private set; } = true;
    public int FailedAccessCount { get; private set; }
    public DateTime? LockoutEndUtc { get; private set; }
    public DateTime? LastLoginAtUtc { get; private set; }
    public DateTime? PasswordChangedAtUtc { get; private set; }
    public void SetUserName(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName)) throw new ArgumentException("User name is required.", nameof(userName));
        var normalized = userName.Trim();
        if (normalized.Length is < 3 or > 80) throw new ArgumentOutOfRangeException(nameof(userName));
        UserName = normalized;
        NormalizedUserName = normalized.ToUpperInvariant();
    }
    public void ChangeRole(Guid roleId)
    {
        if (roleId == Guid.Empty) throw new ArgumentException("A role is required.", nameof(roleId));
        RoleId = roleId;
    }
    public void AssignRole(Role role)
    {
        ArgumentNullException.ThrowIfNull(role);
        ChangeRole(role.Id);
        Role = role;
    }
    public void SetPasswordHash(string passwordHash, DateTime? changedAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(passwordHash)) throw new ArgumentException("A password hash is required.", nameof(passwordHash));
        PasswordHash = passwordHash;
        if (changedAtUtc is not null) PasswordChangedAtUtc = DateTime.SpecifyKind(changedAtUtc.Value, DateTimeKind.Utc);
    }
    public bool IsLockedOut(DateTime nowUtc) => LockoutEndUtc is not null && LockoutEndUtc.Value > DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
    public void RegisterFailedLogin(DateTime nowUtc, int maximumAttempts = 5, int lockoutMinutes = 15)
    {
        if (maximumAttempts < 1) throw new ArgumentOutOfRangeException(nameof(maximumAttempts));
        if (lockoutMinutes < 1) throw new ArgumentOutOfRangeException(nameof(lockoutMinutes));
        var now = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
        if (IsLockedOut(now)) return;
        FailedAccessCount++;
        if (FailedAccessCount >= maximumAttempts)
        {
            LockoutEndUtc = now.AddMinutes(lockoutMinutes);
            FailedAccessCount = 0;
        }
    }
    public void RegisterSuccessfulLogin(DateTime nowUtc)
    {
        FailedAccessCount = 0;
        LockoutEndUtc = null;
        LastLoginAtUtc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
    }
    public void SetActive(bool active) => IsActive = active;
}
