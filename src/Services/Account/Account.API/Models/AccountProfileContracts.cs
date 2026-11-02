using System.ComponentModel.DataAnnotations;
using Account.Domain.Entities;

namespace Account.API.Models;

public sealed record UpdateAccountProfileRequest(
    [property: Required, StringLength(120, MinimumLength = 1)] string DisplayName,
    [property: StringLength(80)] string? FirstName,
    [property: StringLength(80)] string? LastName,
    [property: EmailAddress, StringLength(254)] string? Email,
    [property: Phone, StringLength(40)] string? PhoneNumber,
    [property: StringLength(160)] string? CompanyName);

public sealed record AccountProfileDto(
    Guid UserId, string DisplayName, string? FirstName, string? LastName,
    string? Email, string? PhoneNumber, string? CompanyName, DateTime CreatedDate, DateTime? UpdatedAtUtc)
{
    public static AccountProfileDto From(AccountProfile p) =>
        new(p.Id, p.DisplayName, p.FirstName, p.LastName, p.Email, p.PhoneNumber, p.CompanyName, p.CreatedDate, p.LastModifiedDate);
}

public sealed record UpdateAccountPreferenceRequest(
    [property: Required] string Theme,
    [property: Required] string Language,
    [property: Required, StringLength(80)] string TimeZone,
    bool EmailNotificationsEnabled,
    bool SmsNotificationsEnabled);

public sealed record AccountPreferenceDto(
    Guid UserId, string Theme, string Language, string TimeZone,
    bool EmailNotificationsEnabled, bool SmsNotificationsEnabled)
{
    public static AccountPreferenceDto From(AccountPreference p) =>
        new(p.Id, p.Theme, p.Language, p.TimeZone, p.EmailNotificationsEnabled, p.SmsNotificationsEnabled);
}

public sealed record AccountAddressRequest(
    [property: Required, StringLength(80, MinimumLength = 1)] string Label,
    [property: Required, StringLength(160, MinimumLength = 1)] string Line1,
    [property: StringLength(160)] string? Line2,
    [property: Required, StringLength(100, MinimumLength = 1)] string City,
    [property: StringLength(100)] string? Region,
    [property: StringLength(24)] string? PostalCode,
    [property: Required, StringLength(2, MinimumLength = 2)] string Country,
    bool IsDefault);

public sealed record AccountAddressDto(
    Guid Id, Guid UserId, string Label, string Line1, string? Line2, string City,
    string? Region, string? PostalCode, string Country, bool IsDefault, DateTime CreatedDate)
{
    public static AccountAddressDto From(AccountAddress a) =>
        new(a.Id, a.UserId, a.Label, a.Line1, a.Line2, a.City, a.Region, a.PostalCode, a.Country, a.IsDefault, a.CreatedDate);
}
