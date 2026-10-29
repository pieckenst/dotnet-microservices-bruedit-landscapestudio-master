using Account.Domain.Entities;
using Account.Domain.Enums;
using Xunit;

namespace Account.Tests;

public sealed class AccountDomainTests
{
    [Fact]
    public void DefaultAddressIsUniqueWithinProfile()
    {
        var profile = new AccountProfile(Guid.NewGuid(), "Design client");
        var first = new ContactAddress(Guid.NewGuid(), profile.Id, AddressType.Home, "Home",
            "1 Main Street", null, "Minsk", null, null, "by", true);
        var second = new ContactAddress(Guid.NewGuid(), profile.Id, AddressType.Billing, "Office",
            "2 Garden Road", null, "Minsk", null, null, "BY", true);

        profile.AddAddress(first);
        profile.AddAddress(second);

        Assert.False(first.IsDefault);
        Assert.True(second.IsDefault);
        Assert.Equal("BY", first.CountryCode);
    }

    [Fact]
    public void ProfileRejectsJavascriptAvatarUrlsAndPreferencesRejectInvalidQuietHours()
    {
        var profile = new AccountProfile(Guid.NewGuid(), "Customer");
        Assert.Throws<ArgumentException>(() => profile.Update("Customer", null, null, null, null, "javascript:alert(1)"));

        var notifications = new NotificationPreferences(Guid.NewGuid(), profile.Id);
        Assert.Throws<ArgumentException>(() => notifications.Update(
            true, true, true, true, true, true, new TimeOnly(22, 0), new TimeOnly(22, 0)));
        Assert.Throws<ArgumentException>(() => notifications.Update(
            true, true, true, true, true, true, new TimeOnly(22, 0), null));
    }

    [Fact]
    public void PreferencesNormalizeCultureCurrencyAndEnumsAreTyped()
    {
        var preferences = new AccountPreferences(Guid.NewGuid(), Guid.NewGuid());
        preferences.Update("en_US", "Europe/Minsk", "byn", ThemePreference.Dark, MeasurementSystem.Metric, true);

        Assert.Equal("en-US", preferences.LanguageCode);
        Assert.Equal("BYN", preferences.CurrencyCode);
        Assert.Equal(ThemePreference.Dark, preferences.Theme);
        Assert.Equal(MeasurementSystem.Metric, preferences.MeasurementSystem);
    }
}
