using Account.Domain.Common;

namespace Account.Domain.Entities;

public sealed class AccountPreference : EntityBase
{
    private AccountPreference() { }

    public AccountPreference(Guid userId)
    {
        if (userId == Guid.Empty) throw new ArgumentException("A user id is required.", nameof(userId));
        Id = userId;
        Theme = "system";
        Language = "en";
        TimeZone = "UTC";
        EmailNotificationsEnabled = true;
        SmsNotificationsEnabled = false;
    }

    public string Theme { get; private set; } = "system";
    public string Language { get; private set; } = "en";
    public string TimeZone { get; private set; } = "UTC";
    public bool EmailNotificationsEnabled { get; private set; }
    public bool SmsNotificationsEnabled { get; private set; }

    public void Update(string theme, string language, string timeZone, bool emailNotifications, bool smsNotifications)
    {
        Theme = NormalizeChoice(theme, ["system", "light", "dark"], nameof(theme));
        Language = NormalizeChoice(language, ["en", "ru", "be"], nameof(language));
        if (string.IsNullOrWhiteSpace(timeZone) || timeZone.Trim().Length > 80)
            throw new ArgumentException("A valid time zone is required.", nameof(timeZone));
        TimeZone = timeZone.Trim();
        EmailNotificationsEnabled = emailNotifications;
        SmsNotificationsEnabled = smsNotifications;
    }

    private static string NormalizeChoice(string value, IReadOnlyCollection<string> allowed, string parameter)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A value is required.", parameter);
        var normalized = value.Trim().ToLowerInvariant();
        if (!allowed.Contains(normalized, StringComparer.Ordinal))
            throw new ArgumentException($"Supported values: {string.Join(", ", allowed)}.", parameter);
        return normalized;
    }
}
