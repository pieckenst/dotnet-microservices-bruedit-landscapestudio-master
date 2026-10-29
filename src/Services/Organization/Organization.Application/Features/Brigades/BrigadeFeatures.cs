using FluentValidation;
using MediatR;
using Organization.Application.Contracts;
using Organization.Application.DTOs;
using Organization.Domain.Entities;
using Organization.Domain.Enums;
using Shared.Common.Exceptions;
using Shared.Common.Persistence;

namespace Organization.Application.Features.Brigades;

public sealed record GetBrigadesQuery(int Page = 1, int PageSize = 50, bool IncludeInactive = false) : IRequest<PagedResponse<BrigadeDto>>;
public sealed record CreateBrigadeCommand(string Code, string Name, string Specialization, string Description, Guid? ForepersonEmployeeId) : IRequest<BrigadeDto>;
public sealed record AddBrigadeMemberCommand(Guid BrigadeId, Guid EmployeeId, BrigadeMemberRole Role, DateOnly JoinedDate) : IRequest<BrigadeDto?>;
public sealed record RemoveBrigadeMemberCommand(Guid BrigadeId, Guid EmployeeId, DateOnly LeaveDate) : IRequest<BrigadeDto?>;

public sealed class GetBrigadesQueryValidator : AbstractValidator<GetBrigadesQuery>
{
    public GetBrigadesQueryValidator() { RuleFor(x => x.Page).GreaterThanOrEqualTo(1); RuleFor(x => x.PageSize).InclusiveBetween(1, 200); }
}
public sealed class CreateBrigadeCommandValidator : AbstractValidator<CreateBrigadeCommand>
{
    public CreateBrigadeCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32).Matches("^[a-zA-Z0-9._-]+$");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Specialization).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.ForepersonEmployeeId).Must(id => !id.HasValue || id.Value != Guid.Empty);
    }
}
public sealed class AddBrigadeMemberCommandValidator : AbstractValidator<AddBrigadeMemberCommand>
{
    public AddBrigadeMemberCommandValidator()
    {
        RuleFor(x => x.BrigadeId).NotEmpty(); RuleFor(x => x.EmployeeId).NotEmpty(); RuleFor(x => x.Role).IsInEnum();
        RuleFor(x => x.JoinedDate).LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow));
    }
}
public sealed class RemoveBrigadeMemberCommandValidator : AbstractValidator<RemoveBrigadeMemberCommand>
{
    public RemoveBrigadeMemberCommandValidator()
    {
        RuleFor(x => x.BrigadeId).NotEmpty(); RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.LeaveDate).LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow));
    }
}

public static class BrigadeMapping
{
    public static BrigadeDto ToDto(Brigade brigade) => new(brigade.Id, brigade.Code, brigade.Name,
        brigade.Specialization, brigade.Description, brigade.ForepersonEmployeeId, brigade.IsActive,
        brigade.Members.OrderBy(member => member.LeftDate.HasValue).ThenBy(member => member.JoinedDate)
            .Select(member => new BrigadeMemberDto(member.Id, member.EmployeeId, member.Employee?.FullName ?? string.Empty,
                member.Role, member.JoinedDate, member.LeftDate)).ToArray(),
        brigade.CreatedDate, brigade.LastModifiedDate);
}
public sealed class GetBrigadesQueryHandler(IBrigadeRepository brigades)
    : IRequestHandler<GetBrigadesQuery, PagedResponse<BrigadeDto>>
{
    public async Task<PagedResponse<BrigadeDto>> Handle(GetBrigadesQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var page = await brigades.GetBrigadesAsync(request.Page, request.PageSize, request.IncludeInactive, cancellationToken);
        return new PagedResponse<BrigadeDto>(page.Items.Select(BrigadeMapping.ToDto).ToArray(), page.Page, page.PageSize, page.TotalCount, page.TotalPages);
    }
}
public sealed class CreateBrigadeCommandHandler(
    IBrigadeRepository brigades, Shared.Common.Persistence.IRepositoryBase<Employee> employees)
    : IRequestHandler<CreateBrigadeCommand, BrigadeDto>
{
    public async Task<BrigadeDto> Handle(CreateBrigadeCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.ForepersonEmployeeId.HasValue)
        {
            var foreperson = await employees.GetByIdAsync(request.ForepersonEmployeeId.Value);
            if (foreperson is null || foreperson.Status != EmployeeStatus.Active)
                throw new NotFoundException(nameof(Employee), request.ForepersonEmployeeId.Value);
        }
        var entity = new Brigade(Guid.NewGuid(), request.Code, request.Name, request.Specialization, request.Description, request.ForepersonEmployeeId);
        await brigades.AddAsync(entity, cancellationToken);
        return BrigadeMapping.ToDto(entity);
    }
}
public sealed class AddBrigadeMemberCommandHandler(
    IBrigadeRepository brigades, Shared.Common.Persistence.IRepositoryBase<Employee> employees)
    : IRequestHandler<AddBrigadeMemberCommand, BrigadeDto?>
{
    public async Task<BrigadeDto?> Handle(AddBrigadeMemberCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var brigade = await brigades.GetByIdWithMembersAsync(request.BrigadeId, tracking: true, cancellationToken);
        if (brigade is null) return null;
        var employee = await employees.GetByIdAsync(request.EmployeeId);
        if (employee is null || employee.Status != EmployeeStatus.Active) throw new NotFoundException(nameof(Employee), request.EmployeeId);
        brigade.AddMember(new BrigadeMember(Guid.NewGuid(), brigade.Id, employee.Id, request.Role, request.JoinedDate));
        await brigades.SaveAsync(brigade, cancellationToken);
        brigade = await brigades.GetByIdWithMembersAsync(brigade.Id, tracking: false, cancellationToken)
            ?? throw new InvalidOperationException("Updated brigade could not be reloaded.");
        return BrigadeMapping.ToDto(brigade);
    }
}
public sealed class RemoveBrigadeMemberCommandHandler(IBrigadeRepository brigades)
    : IRequestHandler<RemoveBrigadeMemberCommand, BrigadeDto?>
{
    public async Task<BrigadeDto?> Handle(RemoveBrigadeMemberCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var brigade = await brigades.GetByIdWithMembersAsync(request.BrigadeId, tracking: true, cancellationToken);
        if (brigade is null) return null;
        brigade.RemoveMember(request.EmployeeId, request.LeaveDate);
        await brigades.SaveAsync(brigade, cancellationToken);
        brigade = await brigades.GetByIdWithMembersAsync(brigade.Id, tracking: false, cancellationToken)
            ?? throw new InvalidOperationException("Updated brigade could not be reloaded.");
        return BrigadeMapping.ToDto(brigade);
    }
}
