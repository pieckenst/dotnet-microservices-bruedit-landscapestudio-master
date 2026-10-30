using Account.Domain.Entities;

namespace Account.Application.Contracts.Persistence;

public interface IAccountProfileRepository
{
    Task<AccountProfile?> GetProfileAsync(Guid userId, CancellationToken cancellationToken);
    Task<AccountPreference?> GetPreferenceAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AccountAddress>> GetAddressesAsync(Guid userId, CancellationToken cancellationToken);
    Task<AccountAddress?> GetAddressAsync(Guid addressId, Guid userId, CancellationToken cancellationToken);

    void Add(AccountProfile profile);
    void Add(AccountPreference preference);
    void Add(AccountAddress address);
    void Remove(AccountAddress address);

    Task ClearDefaultAddressesAsync(Guid userId, Guid? exceptAddressId, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
