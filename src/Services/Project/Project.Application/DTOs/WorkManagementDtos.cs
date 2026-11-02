using Project.Domain.Enums;

namespace Project.Application.DTOs;

public sealed record ProjectStageDto(
    Guid Id, Guid ProjectId, string Name, int Sequence, string Description, StageStatus Status,
    DateOnly? PlannedStartDate, DateOnly? PlannedEndDate, DateOnly? ActualStartDate, DateOnly? ActualEndDate,
    DateTime CreatedDate, DateTime? LastModifiedDate);

public sealed record ProjectTaskDto(
    Guid Id, Guid ProjectId, Guid StageId, string Title, string Description, DateOnly? DueDate,
    Guid? AssignedEmployeeId, Guid? AssignedBrigadeId, ProjectTaskStatus Status, DateOnly? CompletedDate,
    DateTime CreatedDate, DateTime? LastModifiedDate);

public sealed record EstimateLineItemDto(
    Guid Id, EstimateLineItemType ItemType, string Description, decimal Quantity, string UnitSymbol,
    decimal UnitPrice, decimal? UnitCost, Guid? CatalogItemId, decimal LineTotal, decimal? LineCost);

public sealed record EstimateDto(
    Guid Id, Guid ProjectId, string Version, string Notes, EstimateStatus Status, DateTime? SubmittedAtUtc,
    DateTime? ApprovedAtUtc, decimal EstimatedTotal, decimal? EstimatedCost,
    IReadOnlyList<EstimateLineItemDto> LineItems, DateTime CreatedDate, DateTime? LastModifiedDate);
