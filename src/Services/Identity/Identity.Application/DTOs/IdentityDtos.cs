using Identity.Domain.Entities;

namespace Identity.Application.DTOs;

public sealed record IdentityUserDto(
    Guid Id, string UserName, string Role, bool IsActive, DateTime CreatedDate, DateTime? LastLoginAtUtc,
    IReadOnlyList<string> Permissions)
{
    public static IdentityUserDto From(UserCredential user)
    {
        ArgumentNullException.ThrowIfNull(user);
        var permissions = user.Role?.RolePermissions
            .Where(item => item.Permission is not null && item.Permission.IsActive)
            .Select(item => item.Permission.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? Array.Empty<string>();
        return new IdentityUserDto(user.Id, user.UserName, user.Role?.Name ?? "Unknown",
            user.IsActive, user.CreatedDate, user.LastLoginAtUtc, permissions);
    }
}
public sealed record AuthResponse(string AccessToken, string TokenType, DateTime ExpiresAtUtc, IdentityUserDto User);
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);
