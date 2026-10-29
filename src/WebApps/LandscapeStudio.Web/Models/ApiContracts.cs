using System.Text.Json;

namespace LandscapeStudio.Web.Models;

public sealed record ApiPage<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);

public sealed record AuthResponse(string AccessToken, string TokenType, DateTime ExpiresAtUtc, JsonElement User);

public sealed record IdentityUserSummary(Guid Id, string UserName, string Role, bool IsActive);


public sealed record EstimateLineItemDto(
    Guid Id, JsonElement ItemType, string Description, decimal Quantity, string UnitSymbol,
    decimal UnitPrice, decimal? UnitCost, Guid? CatalogItemId, decimal LineTotal, decimal? LineCost);

public sealed record EstimateDto(
    Guid Id, Guid ProjectId, string Version, string Notes, JsonElement Status, DateTime? SubmittedAtUtc,
    DateTime? ApprovedAtUtc, decimal EstimatedTotal, decimal? EstimatedCost,
    IReadOnlyList<EstimateLineItemDto> LineItems, DateTime CreatedDate, DateTime? LastModifiedDate);

public sealed record CreateEstimateRequest(Guid ProjectId, string Version, string Notes);

public sealed record AddEstimateLineItemRequest(
    int ItemType, string Description, decimal Quantity, string UnitSymbol,
    decimal UnitPrice, decimal? UnitCost, Guid? CatalogItemId);

public sealed class EstimatesIndexViewModel
{
    public Guid? ProjectId { get; set; }
    public IReadOnlyList<LandscapeProjectDto> Projects { get; set; } = Array.Empty<LandscapeProjectDto>();
    public IReadOnlyList<EstimateDto> Estimates { get; set; } = Array.Empty<EstimateDto>();
    public int Page { get; set; } = 1;
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
    public string? Error { get; set; }
}

public sealed class CreateEstimateViewModel
{
    public Guid ProjectId { get; set; }
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(40, MinimumLength = 1)]
    public string Version { get; set; } = "V1";
    [System.ComponentModel.DataAnnotations.StringLength(4000)]
    public string Notes { get; set; } = string.Empty;
    public IReadOnlyList<LandscapeProjectDto> Projects { get; set; } = Array.Empty<LandscapeProjectDto>();
}

public sealed class EstimateDetailsViewModel
{
    public EstimateDto? Estimate { get; set; }
    public LandscapeProjectDto? Project { get; set; }
    public AddEstimateLineItemViewModel NewLineItem { get; set; } = new();
    public string? Error { get; set; }
}

public sealed class AddEstimateLineItemViewModel
{
    public Guid EstimateId { get; set; }
    [System.ComponentModel.DataAnnotations.Range(0, 4)]
    public int ItemType { get; set; } = 1;
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(500, MinimumLength = 1)]
    public string Description { get; set; } = string.Empty;
    [System.ComponentModel.DataAnnotations.Range(typeof(decimal), "0.001", "1000000000")]
    public decimal Quantity { get; set; } = 1;
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(24, MinimumLength = 1)]
    public string UnitSymbol { get; set; } = "pcs";
    [System.ComponentModel.DataAnnotations.Range(typeof(decimal), "0", "1000000000000")]
    public decimal UnitPrice { get; set; }
    [System.ComponentModel.DataAnnotations.Range(typeof(decimal), "0", "1000000000000")]
    public decimal? UnitCost { get; set; }
    public Guid? CatalogItemId { get; set; }

    public AddEstimateLineItemRequest ToRequest() =>
        new(ItemType, Description, Quantity, UnitSymbol, UnitPrice, UnitCost, CatalogItemId);
}

public sealed record AnalyticsCountViewModel(string Name, long Count);

public sealed record AnalyticsSummaryViewModel(
    DateTime FromUtc, DateTime ToUtc, long TotalEvents,
    IReadOnlyList<AnalyticsCountViewModel> ByService,
    IReadOnlyList<AnalyticsCountViewModel> ByAction);

public sealed record LandscapeProjectDto(
    Guid Id, string ProjectNumber, string Name, Guid ClientId, Guid LandPlotId, string Description,
    JsonElement Status, DateOnly? PlannedStartDate, DateOnly? PlannedEndDate,
    DateOnly? ActualStartDate, DateOnly? ActualEndDate, DateTime CreatedDate, DateTime? LastModifiedDate);

public sealed record ClientDto(
    Guid Id, string DisplayName, string? Email, string? Phone,
    string? BillingAddress, bool IsArchived, DateTime CreatedDate, DateTime? LastModifiedDate);

public sealed record LandPlotDto(
    Guid Id, string Name, string Address, decimal AreaSquareMetres,
    string? SoilType, string? Notes, bool IsArchived, DateTime CreatedDate, DateTime? LastModifiedDate);

public sealed record CreateProjectRequest(
    string ProjectNumber, string Name, Guid ClientId, Guid LandPlotId,
    string Description, DateOnly? PlannedStartDate, DateOnly? PlannedEndDate);

public sealed record AccountProfileDto(
    Guid UserId, string DisplayName, string? FirstName, string? LastName,
    string? Email, string? PhoneNumber, string? CompanyName, DateTime CreatedDate, DateTime? UpdatedAtUtc);

public sealed record AccountPreferenceDto(
    Guid UserId, string Theme, string Language, string TimeZone,
    bool EmailNotificationsEnabled, bool SmsNotificationsEnabled);


public sealed record AccountAddressDto(
    Guid Id, Guid UserId, string Label, string Line1, string? Line2, string City,
    string? Region, string? PostalCode, string Country, bool IsDefault, DateTime CreatedDate);

public sealed record AccountAddressRequest(
    string Label, string Line1, string? Line2, string City, string? Region,
    string? PostalCode, string Country, bool IsDefault);

public sealed class OperationItem
{
    public Dictionary<string, string> Values { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class OperationListViewModel
{
    public string Title { get; init; } = string.Empty;
    public string Resource { get; init; } = string.Empty;
    public IReadOnlyList<string> Columns { get; init; } = Array.Empty<string>();
    public IReadOnlyList<OperationItem> Items { get; init; } = Array.Empty<OperationItem>();
    public string? Error { get; init; }
}

public sealed class AuditListViewModel
{
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public string? SourceService { get; set; }
    public string? Action { get; set; }
    public IReadOnlyList<Dictionary<string, string>> Items { get; set; } = Array.Empty<Dictionary<string, string>>();
    public long TotalCount { get; set; }
    public string? Error { get; set; }
}

public sealed class CreateProjectViewModel
{
    public string ProjectNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid ClientId { get; set; }
    public Guid LandPlotId { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateOnly? PlannedStartDate { get; set; }
    public DateOnly? PlannedEndDate { get; set; }
    public IReadOnlyList<ClientDto> Clients { get; set; } = Array.Empty<ClientDto>();
    public IReadOnlyList<LandPlotDto> LandPlots { get; set; } = Array.Empty<LandPlotDto>();
}
