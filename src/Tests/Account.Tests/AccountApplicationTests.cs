using Account.Application.Features.Profiles;
using Account.Infrastructure.Persistence;
using Account.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Account.Tests;

public sealed class AccountApplicationTests
{
    [Fact]
    public async Task GetProfile_Bootstraps_Profile_And_Default_Preferences()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AccountContext>().UseSqlite(connection).Options;
        await using var context = new AccountContext(options);
        await context.Database.EnsureCreatedAsync();
        var repository = new AccountProfileRepository(context);
        var userId = Guid.NewGuid();

        var profile = await new GetMyProfileQueryHandler(repository).Handle(
            new GetMyProfileQuery(userId, "studio.user"), CancellationToken.None);

        var preferences = await context.AccountPreferences.AsNoTracking().SingleAsync();

        Assert.Equal(userId, profile.Id);
        Assert.Equal("studio.user", profile.DisplayName);
        Assert.Equal("system", preferences.Theme);
        Assert.Equal("en", preferences.Language);
        Assert.True(preferences.EmailNotificationsEnabled);
        Assert.False(preferences.SmsNotificationsEnabled);
        Assert.Null(context.Model.FindEntityType("Account.Domain.Entities.Account"));
    }

    [Fact]
    public async Task Creating_A_New_Default_Address_Clears_The_Previous_Default()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AccountContext>().UseSqlite(connection).Options;
        await using var context = new AccountContext(options);
        await context.Database.EnsureCreatedAsync();
        var repository = new AccountProfileRepository(context);
        var handler = new CreateMyAddressCommandHandler(repository);
        var userId = Guid.NewGuid();

        var first = await handler.Handle(
            new CreateMyAddressCommand(userId, "studio.user", "Office", "1 Green Street",
                null, "Minsk", null, null, "BY", true),
            CancellationToken.None);
        var second = await handler.Handle(
            new CreateMyAddressCommand(userId, "studio.user", "Warehouse", "2 Garden Road",
                null, "Minsk", null, null, "BY", true),
            CancellationToken.None);

        var addresses = await context.AccountAddresses.AsNoTracking()
            .Where(address => address.UserId == userId)
            .ToListAsync();

        Assert.False(addresses.Single(address => address.Id == first.Id).IsDefault);
        Assert.True(addresses.Single(address => address.Id == second.Id).IsDefault);
        Assert.Equal(2, addresses.Count);
    }

    [Fact]
    public async Task Address_Updates_And_Deletes_Are_Scoped_To_The_Owning_Identity_User()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AccountContext>().UseSqlite(connection).Options;
        await using var context = new AccountContext(options);
        await context.Database.EnsureCreatedAsync();
        var repository = new AccountProfileRepository(context);
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var address = await new CreateMyAddressCommandHandler(repository).Handle(
            new CreateMyAddressCommand(ownerId, "owner", "Home", "1 Green Street",
                null, "Minsk", null, null, "BY", false),
            CancellationToken.None);

        var update = await new UpdateMyAddressCommandHandler(repository).Handle(
            new UpdateMyAddressCommand(otherUserId, address.Id, "Home", "9 Other Street",
                null, "Minsk", null, null, "BY", false),
            CancellationToken.None);
        var deleted = await new DeleteMyAddressCommandHandler(repository).Handle(
            new DeleteMyAddressCommand(otherUserId, address.Id), CancellationToken.None);

        Assert.Null(update);
        Assert.False(deleted);
        var unchanged = await context.AccountAddresses.AsNoTracking().SingleAsync();
        Assert.Equal("1 Green Street", unchanged.Line1);
    }
}
