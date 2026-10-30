using Account.Application.Contracts.Persistence;
using Account.Domain.Entities;
using Account.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Account.Infrastructure.Repositories;

public sealed class AccountProfileRepository(AccountContext context) : IAccountProfileRepository
{
    private readonly AccountContext _context = context ?? throw new ArgumentNullException(nameof(context));

    public Task<AccountProfile?> GetProfileAsync(Guid userId, CancellationToken cancellationToken) =>
        _context.AccountProfiles.SingleOrDefaultAsync(profile => profile.Id == userId, cancellationToken);

    public Task<AccountPreference?> GetPreferenceAsync(Guid userId, CancellationToken cancellationToken) =>
        _context.AccountPreferences.SingleOrDefaultAsync(preference => preference.Id == userId, cancellationToken);

    public async Task<IReadOnlyList<AccountAddress>> GetAddressesAsync(Guid userId, CancellationToken cancellationToken) =>
        await _context.AccountAddresses.AsNoTracking()
            .Where(address => address.UserId == userId)
            .OrderByDescending(address => address.IsDefault)
            .ThenBy(address => address.Label)
            .ToArrayAsync(cancellationToken);

    public Task<AccountAddress?> GetAddressAsync(Guid addressId, Guid userId, CancellationToken cancellationToken) =>
        _context.AccountAddresses.SingleOrDefaultAsync(
            address => address.Id == addressId && address.UserId == userId, cancellationToken);

    public void Add(AccountProfile profile) => _context.AccountProfiles.Add(profile);
    public void Add(AccountPreference preference) => _context.AccountPreferences.Add(preference);
    public void Add(AccountAddress address) => _context.AccountAddresses.Add(address);
    public void Remove(AccountAddress address) => _context.AccountAddresses.Remove(address);

    public async Task ClearDefaultAddressesAsync(
        Guid userId, Guid? exceptAddressId, CancellationToken cancellationToken)
    {
        var defaults = await _context.AccountAddresses
            .Where(address => address.UserId == userId && address.IsDefault &&
                (!exceptAddressId.HasValue || address.Id != exceptAddressId.Value))
            .ToListAsync(cancellationToken);

        foreach (var address in defaults)
            address.Update(address.Label, address.Line1, address.Line2, address.City,
                address.Region, address.PostalCode, address.Country, false);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken) =>
        await _context.SaveChangesAsync(cancellationToken);
}
