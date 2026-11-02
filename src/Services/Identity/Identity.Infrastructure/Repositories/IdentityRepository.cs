using Identity.Application.Contracts;
using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Shared.Common.Persistence;

namespace Identity.Infrastructure.Repositories;

public sealed class IdentityRepository(IdentityContext context) : IIdentityRepository
{
    public Task<UserCredential?> FindByNormalizedUserNameAsync(string normalizedUserName, CancellationToken cancellationToken) =>
        context.UserCredentials.Include(user => user.Role)
            .ThenInclude(role => role.RolePermissions).ThenInclude(rolePermission => rolePermission.Permission)
            .SingleOrDefaultAsync(user => user.NormalizedUserName == normalizedUserName, cancellationToken);

    public Task<UserCredential?> GetUserByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.UserCredentials.AsNoTracking().Include(user => user.Role)
            .ThenInclude(role => role.RolePermissions).ThenInclude(rolePermission => rolePermission.Permission)
            .SingleOrDefaultAsync(user => user.Id == id, cancellationToken);

    public Task<Role?> FindRoleByNormalizedNameAsync(string normalizedRoleName, CancellationToken cancellationToken) =>
        context.Roles.Include(role => role.RolePermissions).ThenInclude(rolePermission => rolePermission.Permission)
            .SingleOrDefaultAsync(role => role.NormalizedName == normalizedRoleName, cancellationToken);

    public Task<bool> UserNameExistsAsync(string normalizedUserName, CancellationToken cancellationToken) =>
        context.UserCredentials.AnyAsync(user => user.NormalizedUserName == normalizedUserName, cancellationToken);

    public async Task<PagedResult<UserCredential>> GetUsersPageAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize is < 1 or > 200) throw new ArgumentOutOfRangeException(nameof(pageSize));
        var query = context.UserCredentials.AsNoTracking();
        var totalCount = await query.CountAsync(cancellationToken);
        var users = await query.Include(user => user.Role)
            .ThenInclude(role => role.RolePermissions).ThenInclude(rolePermission => rolePermission.Permission)
            .OrderBy(user => user.NormalizedUserName).Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<UserCredential>(users, page, pageSize, totalCount);
    }

    public async Task AddUserAsync(UserCredential user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        await context.UserCredentials.AddAsync(user, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }
    public async Task UpdateUserAsync(UserCredential user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        context.Entry(user).State = EntityState.Modified;
        await context.SaveChangesAsync(cancellationToken);
    }
}
