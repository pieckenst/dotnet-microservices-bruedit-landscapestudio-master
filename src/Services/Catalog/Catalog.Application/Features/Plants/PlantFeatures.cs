using Catalog.Application.DTOs;
using Catalog.Domain.Entities;
using FluentValidation;
using MediatR;
using Shared.Common.Exceptions;
using Shared.Common.Persistence;

namespace Catalog.Application.Features.Plants;

public sealed record PlantDto(
    Guid Id,
    string Name,
    string? LatinName,
    string Description,
    Guid MeasurementUnitId,
    decimal UnitPrice,
    bool IsActive,
    DateTime CreatedDate,
    DateTime? LastModifiedDate);

public sealed record GetPlantsQuery(int Page = 1, int PageSize = 50) : IRequest<PagedResponse<PlantDto>>;
public sealed record GetPlantByIdQuery(Guid Id) : IRequest<PlantDto?>;

public sealed record CreatePlantCommand(
    string Name,
    string? LatinName,
    string Description,
    Guid MeasurementUnitId,
    decimal UnitPrice,
    bool IsActive = true) : IRequest<PlantDto>;

public sealed record UpdatePlantCommand(
    Guid Id,
    string Name,
    string? LatinName,
    string Description,
    Guid MeasurementUnitId,
    decimal UnitPrice,
    bool IsActive) : IRequest<PlantDto?>;

public sealed record DeletePlantCommand(Guid Id) : IRequest<bool>;

public sealed class GetPlantsQueryValidator : AbstractValidator<GetPlantsQuery>
{
    public GetPlantsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}

public sealed class CreatePlantCommandValidator : AbstractValidator<CreatePlantCommand>
{
    public CreatePlantCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(160);
        RuleFor(x => x.LatinName).MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.MeasurementUnitId).NotEmpty();
        RuleFor(x => x.UnitPrice).InclusiveBetween(0, 1_000_000_000m);
    }
}

public sealed class UpdatePlantCommandValidator : AbstractValidator<UpdatePlantCommand>
{
    public UpdatePlantCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(160);
        RuleFor(x => x.LatinName).MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.MeasurementUnitId).NotEmpty();
        RuleFor(x => x.UnitPrice).InclusiveBetween(0, 1_000_000_000m);
    }
}

public sealed class DeletePlantCommandValidator : AbstractValidator<DeletePlantCommand>
{
    public DeletePlantCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}

public sealed class GetPlantsQueryHandler(IRepositoryBase<Plant> plants)
    : IRequestHandler<GetPlantsQuery, PagedResponse<PlantDto>>
{
    public async Task<PagedResponse<PlantDto>> Handle(GetPlantsQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var page = await plants.GetPagedAsync(request.Page, request.PageSize, orderBy: query => query.OrderBy(x => x.Name));
        return new PagedResponse<PlantDto>(
            page.Items.Select(Map).ToArray(),
            page.Page,
            page.PageSize,
            page.TotalCount,
            page.TotalPages);
    }

    internal static PlantDto Map(Plant entity) => new(
        entity.Id, entity.Name, entity.LatinName, entity.Description,
        entity.MeasurementUnitId, entity.UnitPrice, entity.IsActive,
        entity.CreatedDate, entity.LastModifiedDate);
}

public sealed class GetPlantByIdQueryHandler(IRepositoryBase<Plant> plants)
    : IRequestHandler<GetPlantByIdQuery, PlantDto?>
{
    public async Task<PlantDto?> Handle(GetPlantByIdQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entity = await plants.GetByIdAsync(request.Id);
        return entity is null ? null : GetPlantsQueryHandler.Map(entity);
    }
}

public sealed class CreatePlantCommandHandler(
    IRepositoryBase<Plant> plants,
    IRepositoryBase<MeasurementUnit> units)
    : IRequestHandler<CreatePlantCommand, PlantDto>
{
    public async Task<PlantDto> Handle(CreatePlantCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var unit = await units.GetByIdAsync(request.MeasurementUnitId);
        if (unit is null)
            throw new NotFoundException(nameof(MeasurementUnit), request.MeasurementUnitId);
        if (!unit.IsActive)
            throw new InvalidOperationException("An inactive measurement unit cannot be assigned to a plant.");

        var entity = new Plant(
            Guid.NewGuid(), request.Name, request.LatinName, request.Description,
            request.MeasurementUnitId, request.UnitPrice, request.IsActive);
        await plants.AddAsync(entity);
        return GetPlantsQueryHandler.Map(entity);
    }
}

public sealed class UpdatePlantCommandHandler(
    IRepositoryBase<Plant> plants,
    IRepositoryBase<MeasurementUnit> units)
    : IRequestHandler<UpdatePlantCommand, PlantDto?>
{
    public async Task<PlantDto?> Handle(UpdatePlantCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entity = await plants.GetByIdAsync(request.Id);
        if (entity is null) return null;

        var unit = await units.GetByIdAsync(request.MeasurementUnitId);
        if (unit is null)
            throw new NotFoundException(nameof(MeasurementUnit), request.MeasurementUnitId);
        if (!unit.IsActive)
            throw new InvalidOperationException("An inactive measurement unit cannot be assigned to a plant.");

        entity.Update(request.Name, request.LatinName, request.Description,
            request.MeasurementUnitId, request.UnitPrice, request.IsActive);
        await plants.UpdateAsync(entity);
        return GetPlantsQueryHandler.Map(entity);
    }
}

public sealed class DeletePlantCommandHandler(IRepositoryBase<Plant> plants)
    : IRequestHandler<DeletePlantCommand, bool>
{
    public async Task<bool> Handle(DeletePlantCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entity = await plants.GetByIdAsync(request.Id);
        if (entity is null) return false;
        await plants.DeleteAsync(entity);
        return true;
    }
}
