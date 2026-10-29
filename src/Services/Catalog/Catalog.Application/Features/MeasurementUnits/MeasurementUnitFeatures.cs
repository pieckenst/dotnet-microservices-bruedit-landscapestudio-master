using Catalog.Application.DTOs;
using Catalog.Domain.Entities;
using FluentValidation;
using MediatR;
using Shared.Common.Persistence;

namespace Catalog.Application.Features.MeasurementUnits;

public sealed record MeasurementUnitDto(
    Guid Id,
    string Name,
    string Symbol,
    bool IsActive,
    DateTime CreatedDate,
    DateTime? LastModifiedDate);

public sealed record GetMeasurementUnitsQuery(int Page = 1, int PageSize = 50)
    : IRequest<PagedResponse<MeasurementUnitDto>>;
public sealed record GetMeasurementUnitByIdQuery(Guid Id) : IRequest<MeasurementUnitDto?>;

public sealed record CreateMeasurementUnitCommand(string Name, string Symbol, bool IsActive = true)
    : IRequest<MeasurementUnitDto>;

public sealed record UpdateMeasurementUnitCommand(Guid Id, string Name, string Symbol, bool IsActive)
    : IRequest<MeasurementUnitDto?>;

public sealed record DeleteMeasurementUnitCommand(Guid Id) : IRequest<bool>;

public sealed class GetMeasurementUnitsQueryValidator : AbstractValidator<GetMeasurementUnitsQuery>
{
    public GetMeasurementUnitsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}

public sealed class CreateMeasurementUnitCommandValidator : AbstractValidator<CreateMeasurementUnitCommand>
{
    public CreateMeasurementUnitCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Symbol).NotEmpty().MaximumLength(24);
    }
}

public sealed class UpdateMeasurementUnitCommandValidator : AbstractValidator<UpdateMeasurementUnitCommand>
{
    public UpdateMeasurementUnitCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Symbol).NotEmpty().MaximumLength(24);
    }
}

public sealed class DeleteMeasurementUnitCommandValidator : AbstractValidator<DeleteMeasurementUnitCommand>
{
    public DeleteMeasurementUnitCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}

public sealed class GetMeasurementUnitsQueryHandler(IRepositoryBase<MeasurementUnit> units)
    : IRequestHandler<GetMeasurementUnitsQuery, PagedResponse<MeasurementUnitDto>>
{
    public async Task<PagedResponse<MeasurementUnitDto>> Handle(
        GetMeasurementUnitsQuery request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var page = await units.GetPagedAsync(request.Page, request.PageSize, orderBy: query => query.OrderBy(x => x.Name));
        return new PagedResponse<MeasurementUnitDto>(
            page.Items.Select(Map).ToArray(), page.Page, page.PageSize, page.TotalCount, page.TotalPages);
    }

    internal static MeasurementUnitDto Map(MeasurementUnit entity) => new(
        entity.Id, entity.Name, entity.Symbol, entity.IsActive,
        entity.CreatedDate, entity.LastModifiedDate);
}

public sealed class GetMeasurementUnitByIdQueryHandler(IRepositoryBase<MeasurementUnit> units)
    : IRequestHandler<GetMeasurementUnitByIdQuery, MeasurementUnitDto?>
{
    public async Task<MeasurementUnitDto?> Handle(GetMeasurementUnitByIdQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entity = await units.GetByIdAsync(request.Id);
        return entity is null ? null : GetMeasurementUnitsQueryHandler.Map(entity);
    }
}

public sealed class CreateMeasurementUnitCommandHandler(IRepositoryBase<MeasurementUnit> units)
    : IRequestHandler<CreateMeasurementUnitCommand, MeasurementUnitDto>
{
    public async Task<MeasurementUnitDto> Handle(
        CreateMeasurementUnitCommand request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entity = new MeasurementUnit(Guid.NewGuid(), request.Name, request.Symbol, request.IsActive);
        await units.AddAsync(entity);
        return GetMeasurementUnitsQueryHandler.Map(entity);
    }
}

public sealed class UpdateMeasurementUnitCommandHandler(IRepositoryBase<MeasurementUnit> units)
    : IRequestHandler<UpdateMeasurementUnitCommand, MeasurementUnitDto?>
{
    public async Task<MeasurementUnitDto?> Handle(
        UpdateMeasurementUnitCommand request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entity = await units.GetByIdAsync(request.Id);
        if (entity is null) return null;
        entity.Update(request.Name, request.Symbol, request.IsActive);
        await units.UpdateAsync(entity);
        return GetMeasurementUnitsQueryHandler.Map(entity);
    }
}

public sealed class DeleteMeasurementUnitCommandHandler(IRepositoryBase<MeasurementUnit> units)
    : IRequestHandler<DeleteMeasurementUnitCommand, bool>
{
    public async Task<bool> Handle(DeleteMeasurementUnitCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entity = await units.GetByIdAsync(request.Id);
        if (entity is null) return false;
        await units.DeleteAsync(entity);
        return true;
    }
}
