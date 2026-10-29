using System.Text.Json;

namespace LandscapeStudio.Web.Models;

public sealed record ApiPage<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);

public sealed record AuthResponse(string AccessToken, string TokenType, DateTime ExpiresAtUtc, JsonElement User);

public sealed record IdentityUserSummary(Guid Id, string UserName, string Role, bool IsActive);

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
