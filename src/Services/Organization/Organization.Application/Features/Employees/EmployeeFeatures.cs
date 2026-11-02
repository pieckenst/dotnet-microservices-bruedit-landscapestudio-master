using FluentValidation;
using MediatR;
using Organization.Application.DTOs;
using Organization.Domain.Entities;
using Organization.Domain.Enums;
using Shared.Common.Exceptions;
using Shared.Common.Persistence;

namespace Organization.Application.Features.Employees;

public sealed record GetEmployeesQuery(int Page = 1, int PageSize = 50, EmployeeStatus? Status = null) : IRequest<PagedResponse<EmployeeDto>>;
public sealed record CreateEmployeeCommand(string EmployeeNumber, string FirstName, string LastName, Guid PositionId,
    DateOnly HireDate, Guid? IdentityUserId, string? Email, string? Phone) : IRequest<EmployeeDto>;
public sealed record UpdateEmployeeCommand(Guid Id, string EmployeeNumber, string FirstName, string LastName,
    Guid PositionId, DateOnly HireDate, Guid? IdentityUserId, string? Email, string? Phone) : IRequest<EmployeeDto?>;
public sealed record ChangeEmployeeStatusCommand(Guid Id, EmployeeStatus Status) : IRequest<EmployeeDto?>;

public sealed class GetEmployeesQueryValidator : AbstractValidator<GetEmployeesQuery>
{
    public GetEmployeesQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.Status).Must(status => !status.HasValue || Enum.IsDefined(status.Value));
    }
}
public sealed class CreateEmployeeCommandValidator : AbstractValidator<CreateEmployeeCommand>
{
    public CreateEmployeeCommandValidator()
    {
        RuleFor(x => x.EmployeeNumber).NotEmpty().MaximumLength(32).Matches("^[a-zA-Z0-9._/-]+$");
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PositionId).NotEmpty();
        RuleFor(x => x.HireDate).LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow));
        RuleFor(x => x.IdentityUserId).Must(id => !id.HasValue || id.Value != Guid.Empty);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(254).When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).MaximumLength(40);
    }
}
public sealed class UpdateEmployeeCommandValidator : AbstractValidator<UpdateEmployeeCommand>
{
    public UpdateEmployeeCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.EmployeeNumber).NotEmpty().MaximumLength(32).Matches("^[a-zA-Z0-9._/-]+$");
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PositionId).NotEmpty();
        RuleFor(x => x.HireDate).LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow));
        RuleFor(x => x.IdentityUserId).Must(id => !id.HasValue || id.Value != Guid.Empty);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(254).When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).MaximumLength(40);
    }
}
public sealed class ChangeEmployeeStatusCommandValidator : AbstractValidator<ChangeEmployeeStatusCommand>
{
    public ChangeEmployeeStatusCommandValidator() { RuleFor(x => x.Id).NotEmpty(); RuleFor(x => x.Status).IsInEnum(); }
}
public static class EmployeeMapping
{
    public static EmployeeDto ToDto(Employee x) => new(x.Id, x.EmployeeNumber, x.FirstName, x.LastName,
        x.FullName, x.PositionId, x.IdentityUserId, x.Email, x.Phone, x.HireDate, x.TerminationDate, x.Status,
        x.CreatedDate, x.LastModifiedDate);
}
public sealed class GetEmployeesQueryHandler(IRepositoryBase<Employee> employees)
    : IRequestHandler<GetEmployeesQuery, PagedResponse<EmployeeDto>>
{
    public async Task<PagedResponse<EmployeeDto>> Handle(GetEmployeesQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var page = await employees.GetPagedAsync(request.Page, request.PageSize,
            predicate: item => request.Status == null || item.Status == request.Status,
            orderBy: query => query.OrderBy(item => item.EmployeeNumber));
        return new PagedResponse<EmployeeDto>(page.Items.Select(EmployeeMapping.ToDto).ToArray(), page.Page, page.PageSize, page.TotalCount, page.TotalPages);
    }
}
public sealed class CreateEmployeeCommandHandler(IRepositoryBase<Employee> employees, IRepositoryBase<Position> positions)
    : IRequestHandler<CreateEmployeeCommand, EmployeeDto>
{
    public async Task<EmployeeDto> Handle(CreateEmployeeCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var number = request.EmployeeNumber.Trim().ToUpperInvariant();
        if (await employees.GetAsync(item => item.EmployeeNumber == number) is not null)
            throw new InvalidOperationException("An employee with that number already exists.");
        var position = await positions.GetByIdAsync(request.PositionId);
        if (position is null || !position.IsActive) throw new NotFoundException(nameof(Position), request.PositionId);
        if (request.IdentityUserId.HasValue &&
            await employees.GetAsync(item => item.IdentityUserId == request.IdentityUserId) is not null)
            throw new InvalidOperationException("That Identity user is already linked to an employee.");
        var entity = new Employee(Guid.NewGuid(), request.EmployeeNumber, request.FirstName, request.LastName,
            request.PositionId, request.HireDate, request.IdentityUserId, request.Email, request.Phone);
        await employees.AddAsync(entity);
        return EmployeeMapping.ToDto(entity);
    }
}
public sealed class UpdateEmployeeCommandHandler(IRepositoryBase<Employee> employees, IRepositoryBase<Position> positions)
    : IRequestHandler<UpdateEmployeeCommand, EmployeeDto?>
{
    public async Task<EmployeeDto?> Handle(UpdateEmployeeCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entity = await employees.GetByIdAsync(request.Id);
        if (entity is null) return null;
        if (entity.Status == EmployeeStatus.Terminated) throw new InvalidOperationException("Terminated employee records cannot be edited.");
        var number = request.EmployeeNumber.Trim().ToUpperInvariant();
        if (await employees.GetAsync(other => other.Id != entity.Id && other.EmployeeNumber == number) is not null)
            throw new InvalidOperationException("An employee with that number already exists.");
        var position = await positions.GetByIdAsync(request.PositionId);
        if (position is null || !position.IsActive) throw new NotFoundException(nameof(Position), request.PositionId);
        if (request.IdentityUserId.HasValue &&
            await employees.GetAsync(other => other.Id != entity.Id && other.IdentityUserId == request.IdentityUserId) is not null)
            throw new InvalidOperationException("That Identity user is already linked to an employee.");
        entity.Update(request.EmployeeNumber, request.FirstName, request.LastName, request.PositionId,
            request.HireDate, request.IdentityUserId, request.Email, request.Phone);
        await employees.UpdateAsync(entity);
        return EmployeeMapping.ToDto(entity);
    }
}
public sealed class ChangeEmployeeStatusCommandHandler(IRepositoryBase<Employee> employees)
    : IRequestHandler<ChangeEmployeeStatusCommand, EmployeeDto?>
{
    public async Task<EmployeeDto?> Handle(ChangeEmployeeStatusCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entity = await employees.GetByIdAsync(request.Id);
        if (entity is null) return null;
        entity.ChangeStatus(request.Status, DateOnly.FromDateTime(DateTime.UtcNow));
        await employees.UpdateAsync(entity);
        return EmployeeMapping.ToDto(entity);
    }
}
