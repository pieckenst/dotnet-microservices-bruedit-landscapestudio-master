using Identity.Domain.Entities;
using Shared.Common.Persistence;

namespace Identity.Application.Contracts;

public interface IIdentityRepository
{
    Task<UserCredential?> FindByNormalizedUserNameAsync(string normalizedUserName, CancellationToken cancellationToken);
    Task<UserCredential?> GetUserByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Role?> FindRoleByNormalizedNameAsync(string normalizedRoleName, CancellationToken cancellationToken);
    Task<bool> UserNameExistsAsync(string normalizedUserName, CancellationToken cancellationToken);
    Task<PagedResult<UserCredential>> GetUsersPageAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task AddUserAsync(UserCredential user, CancellationToken cancellationToken);
    Task UpdateUserAsync(UserCredential user, CancellationToken cancellationToken);
}
