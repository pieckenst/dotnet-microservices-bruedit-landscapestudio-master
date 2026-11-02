using Catalog.Application.DTOs;
using Catalog.Domain.Entities;
using FluentValidation;
using MediatR;
using Shared.Common.Exceptions;
using Shared.Common.Persistence;

namespace Catalog.Application.Features.WorkTypes;

public sealed record WorkTypeDto(
    Guid Id,
    string Name,
    string Description,
    Guid MeasurementUnitId,
    decimal BasePrice,
    bool IsActive,
    DateTime CreatedDate,
    DateTime? LastModifiedDate);

public sealed record GetWorkTypesQuery(int Page = 1, int PageSize = 50) : IRequest<PagedResponse<WorkTypeDto>>;
public sealed record GetWorkTypeByIdQuery(Guid Id) : IRequest<WorkTypeDto?>;

public sealed record CreateWorkTypeCommand(
    string Name,
    string Description,
    Guid MeasurementUnitId,
    decimal BasePrice,
    bool IsActive = true) : IRequest<WorkTypeDto>;

public sealed record UpdateWorkTypeCommand(
    Guid Id,
    string Name,
    string Description,
    Guid MeasurementUnitId,
    decimal BasePrice,
    bool IsActive) : IRequest<WorkTypeDto?>;

public sealed record DeleteWorkTypeCommand(Guid Id) : IRequest<bool>;

public sealed class GetWorkTypesQueryValidator : AbstractValidator<GetWorkTypesQuery>
{
    public GetWorkTypesQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}

public sealed class CreateWorkTypeCommandValidator : AbstractValidator<CreateWorkTypeCommand>
{
    public CreateWorkTypeCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.MeasurementUnitId).NotEmpty();
        RuleFor(x => x.BasePrice).InclusiveBetween(0, 1_000_000_000m);
    }
}

public sealed class UpdateWorkTypeCommandValidator : AbstractValidator<UpdateWorkTypeCommand>
{
    public UpdateWorkTypeCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.MeasurementUnitId).NotEmpty();
        RuleFor(x => x.BasePrice).InclusiveBetween(0, 1_000_000_000m);
    }
}

public sealed class DeleteWorkTypeCommandValidator : AbstractValidator<DeleteWorkTypeCommand>
{
    public DeleteWorkTypeCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}

public sealed class GetWorkTypesQueryHandler(IRepositoryBase<WorkType> workTypes)
    : IRequestHandler<GetWorkTypesQuery, PagedResponse<WorkTypeDto>>
{
    public async Task<PagedResponse<WorkTypeDto>> Handle(GetWorkTypesQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var page = await workTypes.GetPagedAsync(request.Page, request.PageSize, orderBy: query => query.OrderBy(x => x.Name));
        return new PagedResponse<WorkTypeDto>(
            page.Items.Select(Map).ToArray(), page.Page, page.PageSize, page.TotalCount, page.TotalPages);
    }

    internal static WorkTypeDto Map(WorkType entity) => new(
        entity.Id, entity.Name, entity.Description, entity.MeasurementUnitId,
        entity.BasePrice, entity.IsActive, entity.CreatedDate, entity.LastModifiedDate);
}

public sealed class GetWorkTypeByIdQueryHandler(IRepositoryBase<WorkType> workTypes)
    : IRequestHandler<GetWorkTypeByIdQuery, WorkTypeDto?>
{
    public async Task<WorkTypeDto?> Handle(GetWorkTypeByIdQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entity = await workTypes.GetByIdAsync(request.Id);
        return entity is null ? null : GetWorkTypesQueryHandler.Map(entity);
    }
}

public sealed class CreateWorkTypeCommandHandler(
    IRepositoryBase<WorkType> workTypes,
    IRepositoryBase<MeasurementUnit> units)
    : IRequestHandler<CreateWorkTypeCommand, WorkTypeDto>
{
    public async Task<WorkTypeDto> Handle(CreateWorkTypeCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var unit = await units.GetByIdAsync(request.MeasurementUnitId);
        if (unit is null) throw new NotFoundException(nameof(MeasurementUnit), request.MeasurementUnitId);
        if (!unit.IsActive)
            throw new InvalidOperationException("An inactive measurement unit cannot be assigned to a work type.");

        var entity = new WorkType(
            Guid.NewGuid(), request.Name, request.Description,
            request.MeasurementUnitId, request.BasePrice, request.IsActive);
        await workTypes.AddAsync(entity);
        return GetWorkTypesQueryHandler.Map(entity);
    }
}

public sealed class UpdateWorkTypeCommandHandler(
    IRepositoryBase<WorkType> workTypes,
    IRepositoryBase<MeasurementUnit> units)
    : IRequestHandler<UpdateWorkTypeCommand, WorkTypeDto?>
{
    public async Task<WorkTypeDto?> Handle(UpdateWorkTypeCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entity = await workTypes.GetByIdAsync(request.Id);
        if (entity is null) return null;
        var unit = await units.GetByIdAsync(request.MeasurementUnitId);
        if (unit is null) throw new NotFoundException(nameof(MeasurementUnit), request.MeasurementUnitId);
        if (!unit.IsActive)
            throw new InvalidOperationException("An inactive measurement unit cannot be assigned to a work type.");

        entity.Update(request.Name, request.Description, request.MeasurementUnitId,
            request.BasePrice, request.IsActive);
        await workTypes.UpdateAsync(entity);
        return GetWorkTypesQueryHandler.Map(entity);
    }
}

public sealed class DeleteWorkTypeCommandHandler(IRepositoryBase<WorkType> workTypes)
    : IRequestHandler<DeleteWorkTypeCommand, bool>
{
    public async Task<bool> Handle(DeleteWorkTypeCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entity = await workTypes.GetByIdAsync(request.Id);
        if (entity is null) return false;
        await workTypes.DeleteAsync(entity);
        return true;
    }
}
