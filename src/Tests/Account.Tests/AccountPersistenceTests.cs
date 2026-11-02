using Account.Domain.Entities;
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

        var userId = Guid.NewGuid(); // Identity subject id; this service stores no credential copy.
        var profile = new AccountProfile(userId, "Landscape customer");
        profile.Update("Landscape customer", "Taylor", "Green", "customer@example.test",
            "+375291234567", "Landscape Studio");
        var preferences = new AccountPreference(userId);
        preferences.Update("light", "en", "Europe/Minsk", true, false);
        var address = new AccountAddress(Guid.NewGuid(), userId, "Home", "1 Green Street", "Minsk", "BY");
        address.Update("Home", "1 Green Street", null, "Minsk", null, "220000", "BY", true);

        await context.AccountProfiles.AddAsync(profile);
        await context.AccountPreferences.AddAsync(preferences);
        await context.AccountAddresses.AddAsync(address);
        await context.SaveChangesAsync();

        var loadedProfile = await context.AccountProfiles.AsNoTracking().SingleAsync(x => x.Id == userId);
        var loadedPreferences = await context.AccountPreferences.AsNoTracking().SingleAsync(x => x.Id == userId);
        var loadedAddress = await context.AccountAddresses.AsNoTracking().SingleAsync(x => x.UserId == userId);

        Assert.Equal("Taylor", loadedProfile.FirstName);
        Assert.Equal("customer@example.test", loadedProfile.Email);
        Assert.Equal("Landscape Studio", loadedProfile.CompanyName);
        Assert.Equal("light", loadedPreferences.Theme);
        Assert.Equal("Europe/Minsk", loadedPreferences.TimeZone);
        Assert.Single(await context.AccountAddresses.AsNoTracking().Where(x => x.UserId == userId).ToListAsync());
        Assert.Equal("BY", loadedAddress.Country);
        Assert.True(loadedAddress.IsDefault);
        Assert.NotEqual(default, loadedProfile.CreatedDate);
        Assert.Null(typeof(AccountProfile).GetProperty("PasswordHash"));
    }
}
