using Organization.Domain.Enums;

namespace Organization.Application.DTOs;

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);
public sealed record PositionDto(Guid Id, string Code, string Title, string Description, int Rank, bool IsActive, DateTime CreatedDate);
public sealed record EmployeeDto(Guid Id, string EmployeeNumber, string FirstName, string LastName, string FullName,
    Guid PositionId, Guid? IdentityUserId, string? Email, string? Phone, DateOnly HireDate, DateOnly? TerminationDate,
    EmployeeStatus Status, DateTime CreatedDate, DateTime? LastModifiedDate);
public sealed record BrigadeMemberDto(Guid Id, Guid EmployeeId, string EmployeeFullName, BrigadeMemberRole Role,
    DateOnly JoinedDate, DateOnly? LeftDate);
public sealed record BrigadeDto(Guid Id, string Code, string Name, string Specialization, string Description,
    Guid? ForepersonEmployeeId, bool IsActive, IReadOnlyList<BrigadeMemberDto> Members,
    DateTime CreatedDate, DateTime? LastModifiedDate);
public sealed record WorkAssignmentDto(Guid Id, Guid? EmployeeId, Guid? BrigadeId, Guid ProjectId, Guid? ProjectTaskId,
    string Title, DateOnly StartDate, DateOnly? EndDate, AssignmentStatus Status, string Notes,
    DateTime CreatedDate, DateTime? LastModifiedDate);
