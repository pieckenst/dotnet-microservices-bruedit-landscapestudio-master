using Account.Domain.Common;

namespace Account.Domain.Entities;

/// <summary>Contact/profile data keyed by the Identity user id; contains no credentials.</summary>
public sealed class AccountProfile : EntityBase
{
    private AccountProfile() { }

    public AccountProfile(Guid userId, string displayName)
    {
        if (userId == Guid.Empty) throw new ArgumentException("A user id is required.", nameof(userId));
        Id = userId;
        SetDisplayName(displayName);
    }

    public string DisplayName { get; private set; } = string.Empty;
    public string? FirstName { get; private set; }
    public string? LastName { get; private set; }
    public string? Email { get; private set; }
    public string? PhoneNumber { get; private set; }
    public string? CompanyName { get; private set; }

    public void Update(string displayName, string? firstName, string? lastName,
        string? email, string? phoneNumber, string? companyName)
    {
        SetDisplayName(displayName);
        FirstName = Clean(firstName, 80);
        LastName = Clean(lastName, 80);
        Email = Clean(email, 254);
        PhoneNumber = Clean(phoneNumber, 40);
        CompanyName = Clean(companyName, 160);
    }

    public void SetDisplayName(string displayName)
    {
        var value = Clean(displayName, 120);
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("A display name is required.", nameof(displayName));
        DisplayName = value;
    }

    private static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
            throw new ArgumentOutOfRangeException(nameof(value), $"Value cannot exceed {maxLength} characters.");
        return trimmed;
    }
}
