using Project.Domain.Enums;

namespace Project.Application.DTOs;

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);

public sealed record LandPlotDto(Guid Id, string Name, string Address, decimal AreaSquareMetres,
    string? SoilType, string? Notes, bool IsArchived, DateTime CreatedDate, DateTime? LastModifiedDate);

public sealed record ClientDto(Guid Id, string DisplayName, string? Email, string? Phone,
    string? BillingAddress, bool IsArchived, DateTime CreatedDate, DateTime? LastModifiedDate);

public sealed record LandscapeProjectDto(
    Guid Id, string ProjectNumber, string Name, Guid ClientId, Guid LandPlotId, string Description,
    ProjectStatus Status, DateOnly? PlannedStartDate, DateOnly? PlannedEndDate,
    DateOnly? ActualStartDate, DateOnly? ActualEndDate, DateTime CreatedDate, DateTime? LastModifiedDate);
