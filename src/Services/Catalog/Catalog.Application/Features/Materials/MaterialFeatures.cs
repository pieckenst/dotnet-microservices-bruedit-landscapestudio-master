using Catalog.Application.DTOs;
using Catalog.Domain.Entities;
using FluentValidation;
using MediatR;
using Shared.Common.Exceptions;
using Shared.Common.Persistence;

namespace Catalog.Application.Features.Materials;

public sealed record MaterialDto(
    Guid Id,
    string Name,
    string Sku,
    string Description,
    Guid MeasurementUnitId,
    decimal UnitPrice,
    bool IsActive,
    DateTime CreatedDate,
    DateTime? LastModifiedDate);

public sealed record GetMaterialsQuery(int Page = 1, int PageSize = 50) : IRequest<PagedResponse<MaterialDto>>;
public sealed record GetMaterialByIdQuery(Guid Id) : IRequest<MaterialDto?>;

public sealed record CreateMaterialCommand(
    string Name,
    string Sku,
    string Description,
    Guid MeasurementUnitId,
    decimal UnitPrice,
    bool IsActive = true) : IRequest<MaterialDto>;

public sealed record UpdateMaterialCommand(
    Guid Id,
    string Name,
    string Sku,
    string Description,
    Guid MeasurementUnitId,
    decimal UnitPrice,
    bool IsActive) : IRequest<MaterialDto?>;

public sealed record DeleteMaterialCommand(Guid Id) : IRequest<bool>;

public sealed class GetMaterialsQueryValidator : AbstractValidator<GetMaterialsQuery>
{
    public GetMaterialsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}

public sealed class CreateMaterialCommandValidator : AbstractValidator<CreateMaterialCommand>
{
    public CreateMaterialCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.MeasurementUnitId).NotEmpty();
        RuleFor(x => x.UnitPrice).InclusiveBetween(0, 1_000_000_000m);
    }
}

public sealed class UpdateMaterialCommandValidator : AbstractValidator<UpdateMaterialCommand>
{
    public UpdateMaterialCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.MeasurementUnitId).NotEmpty();
        RuleFor(x => x.UnitPrice).InclusiveBetween(0, 1_000_000_000m);
    }
}

public sealed class DeleteMaterialCommandValidator : AbstractValidator<DeleteMaterialCommand>
{
    public DeleteMaterialCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}

public sealed class GetMaterialsQueryHandler(IRepositoryBase<Material> materials)
    : IRequestHandler<GetMaterialsQuery, PagedResponse<MaterialDto>>
{
    public async Task<PagedResponse<MaterialDto>> Handle(GetMaterialsQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var page = await materials.GetPagedAsync(request.Page, request.PageSize, orderBy: query => query.OrderBy(x => x.Name));
        return new PagedResponse<MaterialDto>(
            page.Items.Select(Map).ToArray(), page.Page, page.PageSize, page.TotalCount, page.TotalPages);
    }

    internal static MaterialDto Map(Material entity) => new(
        entity.Id, entity.Name, entity.Sku, entity.Description,
        entity.MeasurementUnitId, entity.UnitPrice, entity.IsActive,
        entity.CreatedDate, entity.LastModifiedDate);
}

public sealed class GetMaterialByIdQueryHandler(IRepositoryBase<Material> materials)
    : IRequestHandler<GetMaterialByIdQuery, MaterialDto?>
{
    public async Task<MaterialDto?> Handle(GetMaterialByIdQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entity = await materials.GetByIdAsync(request.Id);
        return entity is null ? null : GetMaterialsQueryHandler.Map(entity);
    }
}

public sealed class CreateMaterialCommandHandler(
    IRepositoryBase<Material> materials,
    IRepositoryBase<MeasurementUnit> units)
    : IRequestHandler<CreateMaterialCommand, MaterialDto>
{
    public async Task<MaterialDto> Handle(CreateMaterialCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var unit = await units.GetByIdAsync(request.MeasurementUnitId);
        if (unit is null) throw new NotFoundException(nameof(MeasurementUnit), request.MeasurementUnitId);
        if (!unit.IsActive)
            throw new InvalidOperationException("An inactive measurement unit cannot be assigned to a material.");

        var entity = new Material(
            Guid.NewGuid(), request.Name, request.Sku, request.Description,
            request.MeasurementUnitId, request.UnitPrice, request.IsActive);
        await materials.AddAsync(entity);
        return GetMaterialsQueryHandler.Map(entity);
    }
}

public sealed class UpdateMaterialCommandHandler(
    IRepositoryBase<Material> materials,
    IRepositoryBase<MeasurementUnit> units)
    : IRequestHandler<UpdateMaterialCommand, MaterialDto?>
{
    public async Task<MaterialDto?> Handle(UpdateMaterialCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entity = await materials.GetByIdAsync(request.Id);
        if (entity is null) return null;
        var unit = await units.GetByIdAsync(request.MeasurementUnitId);
        if (unit is null) throw new NotFoundException(nameof(MeasurementUnit), request.MeasurementUnitId);
        if (!unit.IsActive)
            throw new InvalidOperationException("An inactive measurement unit cannot be assigned to a material.");

        entity.Update(request.Name, request.Sku, request.Description,
            request.MeasurementUnitId, request.UnitPrice, request.IsActive);
        await materials.UpdateAsync(entity);
        return GetMaterialsQueryHandler.Map(entity);
    }
}

public sealed class DeleteMaterialCommandHandler(IRepositoryBase<Material> materials)
    : IRequestHandler<DeleteMaterialCommand, bool>
{
    public async Task<bool> Handle(DeleteMaterialCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entity = await materials.GetByIdAsync(request.Id);
        if (entity is null) return false;
        await materials.DeleteAsync(entity);
        return true;
    }
}
