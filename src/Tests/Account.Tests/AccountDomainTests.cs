using Account.Domain.Entities;
using Xunit;

namespace Account.Tests;

public sealed class AccountDomainTests
{
    [Fact]
    public void Profile_Normalizes_Optional_Contact_Fields_And_Rejects_Blank_DisplayName()
    {
        var profile = new AccountProfile(Guid.NewGuid(), "  Landscape client  ");
        profile.Update("  Landscape client  ", "  Morgan ", " Park  ",
            "morgan@example.test", " +375 29 1234567 ", " Studio North ");

        Assert.Equal("Landscape client", profile.DisplayName);
        Assert.Equal("Morgan", profile.FirstName);
        Assert.Equal("Park", profile.LastName);
        Assert.Equal("morgan@example.test", profile.Email);
        Assert.Equal("+375 29 1234567", profile.PhoneNumber);
        Assert.Equal("Studio North", profile.CompanyName);
        Assert.Throws<ArgumentException>(() => profile.SetDisplayName("  "));
    }

    [Fact]
    public void Preferences_Validate_Allowed_Theme_Language_And_TimeZone()
    {
        var preferences = new AccountPreference(Guid.NewGuid());
        Assert.Equal("system", preferences.Theme);
        Assert.Equal("en", preferences.Language);
        Assert.Equal("UTC", preferences.TimeZone);
        Assert.True(preferences.EmailNotificationsEnabled);
        Assert.False(preferences.SmsNotificationsEnabled);

        preferences.Update(" DARK ", "ru", "Europe/Minsk", emailNotifications: false, smsNotifications: true);
        Assert.Equal("dark", preferences.Theme);
        Assert.Equal("ru", preferences.Language);
        Assert.Equal("Europe/Minsk", preferences.TimeZone);
        Assert.False(preferences.EmailNotificationsEnabled);
        Assert.True(preferences.SmsNotificationsEnabled);

        Assert.Throws<ArgumentException>(() => preferences.Update("sepia", "ru", "UTC", true, false));
        Assert.Throws<ArgumentException>(() => preferences.Update("dark", "fr", "UTC", true, false));
        Assert.Throws<ArgumentException>(() => preferences.Update("dark", "ru", " ", true, false));
    }

    [Fact]
    public void Address_Normalizes_Country_And_Rejects_Invalid_Required_Fields()
    {
        var userId = Guid.NewGuid();
        var address = new AccountAddress(Guid.NewGuid(), userId, "Home", "15 Green Street", "Minsk", "by");
        address.Update(" Home ", " 15 Green Street ", null, " Minsk ", null, "220000", "by", true);

        Assert.Equal(userId, address.UserId);
        Assert.Equal("BY", address.Country);
        Assert.True(address.IsDefault);
        Assert.Equal("220000", address.PostalCode);
        Assert.Throws<ArgumentException>(() => address.Update("", "15 Green Street", null, "Minsk", null, null, "BY", false));
        Assert.Throws<ArgumentException>(() => address.Update("Home", "15 Green Street", null, "Minsk", null, null, "Belarus", false));
    }

    [Fact]
    public void Profile_Has_No_Identity_Credential_Fields()
    {
        Assert.Null(typeof(AccountProfile).GetProperty("PasswordHash"));
        Assert.Null(typeof(AccountProfile).GetProperty("Password"));
        Assert.Null(typeof(AccountProfile).GetProperty("Role"));
        Assert.Null(typeof(AccountProfile).GetProperty("SecurityStamp"));
    }
}
