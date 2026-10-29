using Account.Domain.Entities;
using Account.Domain.Enums;
using Account.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Account.Tests;

public sealed class AccountPersistenceTests
{
    [Fact]
    public async Task ProfilePreferencesAndAddressesPersistWithoutCredentialFields()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AccountContext>().UseSqlite(connection).Options;
        await using var context = new AccountContext(options);
        await context.Database.EnsureCreatedAsync();

        var userId = Guid.NewGuid(); // Same subject id as Identity; no local credential copy.
        var profile = new AccountProfile(userId, "Landscape customer", "customer@example.test");
        profile.Update("Landscape customer", "Taylor", "Green", "customer@example.test", "+375291234567", null);
        profile.SetPreferences(new AccountPreferences(Guid.NewGuid(), userId));
        profile.SetNotifications(new NotificationPreferences(Guid.NewGuid(), userId));
        profile.AddAddress(new ContactAddress(Guid.NewGuid(), userId, AddressType.Home, "Home",
            "1 Green Street", null, "Minsk", null, "220000", "BY", true));

        context.Profiles.Add(profile);
        await context.SaveChangesAsync();

        var loaded = await context.Profiles
            .Include(x => x.Addresses)
            .Include(x => x.Preferences)
            .Include(x => x.Notifications)
            .AsNoTracking()
            .SingleAsync(x => x.Id == userId);

        Assert.Equal("Taylor", loaded.FirstName);
        Assert.Single(loaded.Addresses);
        Assert.Equal("BY", loaded.Addresses.Single().CountryCode);
        Assert.NotNull(loaded.Preferences);
        Assert.NotNull(loaded.Notifications);
        Assert.NotEqual(default, loaded.CreatedDate);
        Assert.Null(typeof(AccountProfile).GetProperty("PasswordHash"));
    }
}
