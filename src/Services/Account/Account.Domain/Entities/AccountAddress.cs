using Account.Domain.Common;

namespace Account.Domain.Entities;

public sealed class AccountAddress : EntityBase
{
    private AccountAddress() { }

    public AccountAddress(Guid id, Guid userId, string label, string line1, string city, string country)
    {
        if (id == Guid.Empty) throw new ArgumentException("An address id is required.", nameof(id));
        if (userId == Guid.Empty) throw new ArgumentException("A user id is required.", nameof(userId));
        Id = id;
        UserId = userId;
        Update(label, line1, null, city, null, null, country, false);
    }

    public Guid UserId { get; private set; }
    public string Label { get; private set; } = string.Empty;
    public string Line1 { get; private set; } = string.Empty;
    public string? Line2 { get; private set; }
    public string City { get; private set; } = string.Empty;
    public string? Region { get; private set; }
    public string? PostalCode { get; private set; }
    public string Country { get; private set; } = string.Empty;
    public bool IsDefault { get; private set; }

    public void Update(string label, string line1, string? line2, string city, string? region,
        string? postalCode, string country, bool isDefault)
    {
        Label = Required(label, 80, nameof(label));
        Line1 = Required(line1, 160, nameof(line1));
        Line2 = Optional(line2, 160, nameof(line2));
        City = Required(city, 100, nameof(city));
        Region = Optional(region, 100, nameof(region));
        PostalCode = Optional(postalCode, 24, nameof(postalCode));
        Country = Required(country, 2, nameof(country)).ToUpperInvariant();
        IsDefault = isDefault;
    }

    private static string Required(string value, int limit, string name)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > limit)
            throw new ArgumentException($"A value of 1 to {limit} characters is required.", name);
        return normalized;
    }

    private static string? Optional(string? value, int limit, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        if (normalized.Length > limit) throw new ArgumentOutOfRangeException(name, $"Maximum length is {limit}.");
        return normalized;
    }
}
