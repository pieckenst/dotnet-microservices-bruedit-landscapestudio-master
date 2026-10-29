using FluentValidation;
using MediatR;
using Organization.Application.DTOs;
using Organization.Domain.Entities;
using Organization.Domain.Enums;
using Shared.Common.Exceptions;
using Shared.Common.Persistence;

namespace Organization.Application.Features.Assignments;

public sealed record GetWorkAssignmentsQuery(int Page = 1, int PageSize = 50, Guid? ProjectId = null,
    AssignmentStatus? Status = null) : IRequest<PagedResponse<WorkAssignmentDto>>;
public sealed record CreateWorkAssignmentCommand(Guid? EmployeeId, Guid? BrigadeId, Guid ProjectId, Guid? ProjectTaskId,
    string Title, DateOnly StartDate, DateOnly? EndDate, string Notes) : IRequest<WorkAssignmentDto>;
public sealed record ChangeWorkAssignmentStatusCommand(Guid Id, AssignmentStatus Status) : IRequest<WorkAssignmentDto?>;

public sealed class GetWorkAssignmentsQueryValidator : AbstractValidator<GetWorkAssignmentsQuery>
{
    public GetWorkAssignmentsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1); RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.Status).Must(value => !value.HasValue || Enum.IsDefined(value.Value));
        RuleFor(x => x.ProjectId).Must(value => !value.HasValue || value.Value != Guid.Empty);
    }
}
public sealed class CreateWorkAssignmentCommandValidator : AbstractValidator<CreateWorkAssignmentCommand>
{
    public CreateWorkAssignmentCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty(); RuleFor(x => x.Title).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Notes).MaximumLength(2000);
        RuleFor(x => x).Must(x => (x.EmployeeId is null) != (x.BrigadeId is null))
            .WithMessage("Assignment must belong to exactly one employee or brigade.");
        RuleFor(x => x.EmployeeId).Must(id => !id.HasValue || id.Value != Guid.Empty);
        RuleFor(x => x.BrigadeId).Must(id => !id.HasValue || id.Value != Guid.Empty);
        RuleFor(x => x.ProjectTaskId).Must(id => !id.HasValue || id.Value != Guid.Empty);
        RuleFor(x => x.EndDate).Must((request, end) => end is null || end >= request.StartDate)
            .WithMessage("End date cannot precede start date.");
    }
}
public sealed class ChangeWorkAssignmentStatusCommandValidator : AbstractValidator<ChangeWorkAssignmentStatusCommand>
{
    public ChangeWorkAssignmentStatusCommandValidator() { RuleFor(x => x.Id).NotEmpty(); RuleFor(x => x.Status).IsInEnum(); }
}
public static class WorkAssignmentMapping
{
    public static WorkAssignmentDto ToDto(WorkAssignment x) => new(x.Id, x.EmployeeId, x.BrigadeId, x.ProjectId,
        x.ProjectTaskId, x.Title, x.StartDate, x.EndDate, x.Status, x.Notes, x.CreatedDate, x.LastModifiedDate);
}
public sealed class GetWorkAssignmentsQueryHandler(IRepositoryBase<WorkAssignment> assignments)
    : IRequestHandler<GetWorkAssignmentsQuery, PagedResponse<WorkAssignmentDto>>
{
    public async Task<PagedResponse<WorkAssignmentDto>> Handle(GetWorkAssignmentsQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var page = await assignments.GetPagedAsync(request.Page, request.PageSize,
            predicate: item => (request.ProjectId == null || item.ProjectId == request.ProjectId) &&
                (request.Status == null || item.Status == request.Status),
            orderBy: query => query.OrderBy(item => item.StartDate).ThenBy(item => item.Title));
        return new PagedResponse<WorkAssignmentDto>(page.Items.Select(WorkAssignmentMapping.ToDto).ToArray(), page.Page, page.PageSize, page.TotalCount, page.TotalPages);
    }
}
public sealed class CreateWorkAssignmentCommandHandler(
    IRepositoryBase<WorkAssignment> assignments, IRepositoryBase<Employee> employees, IRepositoryBase<Brigade> brigades)
    : IRequestHandler<CreateWorkAssignmentCommand, WorkAssignmentDto>
{
    public async Task<WorkAssignmentDto> Handle(CreateWorkAssignmentCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.EmployeeId.HasValue)
        {
            var employee = await employees.GetByIdAsync(request.EmployeeId.Value);
            if (employee is null || employee.Status != EmployeeStatus.Active) throw new NotFoundException(nameof(Employee), request.EmployeeId.Value);
        }
        if (request.BrigadeId.HasValue)
        {
            var brigade = await brigades.GetByIdAsync(request.BrigadeId.Value);
            if (brigade is null || !brigade.IsActive) throw new NotFoundException(nameof(Brigade), request.BrigadeId.Value);
        }
        var entity = new WorkAssignment(Guid.NewGuid(), request.EmployeeId, request.BrigadeId, request.ProjectId,
            request.ProjectTaskId, request.Title, request.StartDate, request.EndDate, request.Notes);
        await assignments.AddAsync(entity);
        return WorkAssignmentMapping.ToDto(entity);
    }
}
public sealed class ChangeWorkAssignmentStatusCommandHandler(IRepositoryBase<WorkAssignment> assignments)
    : IRequestHandler<ChangeWorkAssignmentStatusCommand, WorkAssignmentDto?>
{
    public async Task<WorkAssignmentDto?> Handle(ChangeWorkAssignmentStatusCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entity = await assignments.GetByIdAsync(request.Id);
        if (entity is null) return null;
        entity.ChangeStatus(request.Status);
        await assignments.UpdateAsync(entity);
        return WorkAssignmentMapping.ToDto(entity);
    }
}
