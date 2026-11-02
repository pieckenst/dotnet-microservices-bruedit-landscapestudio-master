using Account.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Account.Infrastructure.Persistence;

public sealed class AccountContextSeed
{
    private static readonly Guid DemoAdminUserId = Guid.Parse("c8fb4b6e-2a81-4d94-b331-2b00b2231001");
    private static readonly Guid DemoStandardUserId = Guid.Parse("c8fb4b6e-2a81-4d94-b331-2b00b2231002");

    public static async Task SeedAsync(
        AccountContext context,
        ILogger<AccountContextSeed> logger,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

        await SeedProfileAsync(context, DemoAdminUserId, "Anna Petrov", "Anna", "Petrov",
            "anna.petrov@landscapestudio.example", "+375 29 123 45 67", "Landscape Studio HQ",
            cancellationToken);
        await SeedProfileAsync(context, DemoStandardUserId, "Sergei Ivanov", "Sergei", "Ivanov",
            "sergei.ivanov@landscapestudio.example", "+375 29 765 43 21", null,
            cancellationToken);

        await SeedPreferenceAsync(context, DemoAdminUserId, "dark", "ru", "Europe/Minsk",
            emailNotifications: true, smsNotifications: false, cancellationToken);
        await SeedPreferenceAsync(context, DemoStandardUserId, "system", "en", "UTC",
            emailNotifications: true, smsNotifications: true, cancellationToken);

        await SeedAddressAsync(context, Guid.Parse("a0000000-0000-0000-0000-000000000001"),
            DemoAdminUserId, "Office", "pr. Nezavisimosti 58", "Minsk", "BY",
            region: "Minsk region", postalCode: "220000", isDefault: true, cancellationToken);
        await SeedAddressAsync(context, Guid.Parse("a0000000-0000-0000-0000-000000000002"),
            DemoStandardUserId, "Home", "ul. Kirova 12", "Brest", "BY",
            region: "Brest region", postalCode: "224050", isDefault: true, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Account seed completed: 2 profiles, 2 preferences, 2 addresses.");
    }

    private static async Task SeedProfileAsync(AccountContext context, Guid userId,
        string displayName, string firstName, string lastName,
        string? email, string? phone, string? companyName,
        CancellationToken cancellationToken)
    {
        if (await context.AccountProfiles.AnyAsync(p => p.Id == userId, cancellationToken))
            return;

        var profile = new AccountProfile(userId, displayName);
        profile.Update(displayName, firstName, lastName, email, phone, companyName);
        context.AccountProfiles.Add(profile);
    }

    private static async Task SeedPreferenceAsync(AccountContext context, Guid userId,
        string theme, string language, string timeZone,
        bool emailNotifications, bool smsNotifications,
        CancellationToken cancellationToken)
    {
        if (await context.AccountPreferences.AnyAsync(p => p.Id == userId, cancellationToken))
            return;

        var preference = new AccountPreference(userId);
        preference.Update(theme, language, timeZone, emailNotifications, smsNotifications);
        context.AccountPreferences.Add(preference);
    }

    private static async Task SeedAddressAsync(AccountContext context, Guid addressId,
        Guid userId, string label, string line1, string city, string country,
        string? region, string? postalCode, bool isDefault,
        CancellationToken cancellationToken)
    {
        if (await context.AccountAddresses.AnyAsync(a => a.Id == addressId, cancellationToken))
            return;

        var address = new AccountAddress(addressId, userId, label, line1, city, country);
        address.Update(label, line1, null, city, region, postalCode, country, isDefault);
        context.AccountAddresses.Add(address);
    }
}
