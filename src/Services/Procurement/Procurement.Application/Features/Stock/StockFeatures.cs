using FluentValidation;
using MediatR;
using Procurement.Application.Contracts;
using Procurement.Application.DTOs;
using Procurement.Domain.Entities;
using Procurement.Domain.Enums;
using Shared.Common.Persistence;

namespace Procurement.Application.Features.Stock;

public sealed record GetStockItemsQuery(int Page = 1, int PageSize = 50, bool BelowReorderLevel = false)
    : IRequest<PagedResult<StockItemDto>>;
public sealed record GetStockItemByIdQuery(Guid Id) : IRequest<StockItemDto?>;
public sealed record CreateStockItemCommand(Guid? CatalogItemId, string Sku, string Description,
    string UnitSymbol, string StorageLocation, decimal ReorderLevel) : IRequest<StockItemDto>;
public sealed record RecordStockMovementCommand(Guid StockItemId, StockMovementType MovementType,
    decimal Quantity, string SourceDocument, Guid? ProjectId = null, string? Notes = null) : IRequest<StockMovementDto>;
public sealed record GetStockMovementsQuery(Guid StockItemId, int Page = 1, int PageSize = 50)
    : IRequest<PagedResult<StockMovementDto>>;

public sealed class GetStockItemsQueryValidator : AbstractValidator<GetStockItemsQuery>
{
    public GetStockItemsQueryValidator() { RuleFor(x => x.Page).GreaterThanOrEqualTo(1); RuleFor(x => x.PageSize).InclusiveBetween(1, 200); }
}
public sealed class GetStockItemByIdQueryValidator : AbstractValidator<GetStockItemByIdQuery>
{
    public GetStockItemByIdQueryValidator() => RuleFor(x => x.Id).NotEmpty();
}
public sealed class CreateStockItemCommandValidator : AbstractValidator<CreateStockItemCommand>
{
    public CreateStockItemCommandValidator()
    {
        RuleFor(x => x.CatalogItemId).Must(id => !id.HasValue || id.Value != Guid.Empty);
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(48);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(200);
        RuleFor(x => x.UnitSymbol).NotEmpty().MaximumLength(24);
        RuleFor(x => x.StorageLocation).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ReorderLevel).InclusiveBetween(0, 1_000_000_000m);
    }
}
public sealed class RecordStockMovementCommandValidator : AbstractValidator<RecordStockMovementCommand>
{
    public RecordStockMovementCommandValidator()
    {
        RuleFor(x => x.StockItemId).NotEmpty();
        RuleFor(x => x.MovementType).IsInEnum();
        RuleFor(x => x.Quantity).GreaterThan(0).LessThanOrEqualTo(1_000_000_000m);
        RuleFor(x => x.SourceDocument).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ProjectId).Must(id => !id.HasValue || id.Value != Guid.Empty);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}
public sealed class GetStockMovementsQueryValidator : AbstractValidator<GetStockMovementsQuery>
{
    public GetStockMovementsQueryValidator()
    {
        RuleFor(x => x.StockItemId).NotEmpty();
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}

public static class StockMapping
{
    public static StockItemDto ToDto(StockItem x) => new(x.Id, x.CatalogItemId, x.Sku, x.Description,
        x.UnitSymbol, x.StorageLocation, x.QuantityOnHand, x.ReorderLevel, x.AvailableQuantity,
        x.IsBelowReorderLevel, x.CreatedDate, x.LastModifiedDate);
    public static StockMovementDto ToDto(StockMovement x) => new(x.Id, x.StockItemId, x.MovementType,
        x.Quantity, x.SourceDocument, x.ProjectId, x.Notes, x.CreatedDate);
}

public sealed class GetStockItemsQueryHandler(IRepositoryBase<StockItem> stockItems)
    : IRequestHandler<GetStockItemsQuery, PagedResult<StockItemDto>>
{
    public async Task<PagedResult<StockItemDto>> Handle(GetStockItemsQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var page = await stockItems.GetPagedAsync(request.Page, request.PageSize,
            predicate: item => !request.BelowReorderLevel || item.QuantityOnHand <= item.ReorderLevel,
            orderBy: query => query.OrderBy(item => item.Sku));
        return new PagedResult<StockItemDto>(page.Items.Select(StockMapping.ToDto).ToArray(),
            page.Page, page.PageSize, page.TotalCount);
    }
}

public sealed class GetStockItemByIdQueryHandler(IRepositoryBase<StockItem> stockItems)
    : IRequestHandler<GetStockItemByIdQuery, StockItemDto?>
{
    public async Task<StockItemDto?> Handle(GetStockItemByIdQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entity = await stockItems.GetByIdAsync(request.Id);
        return entity is null ? null : StockMapping.ToDto(entity);
    }
}

public sealed class CreateStockItemCommandHandler(IRepositoryBase<StockItem> stockItems)
    : IRequestHandler<CreateStockItemCommand, StockItemDto>
{
    public async Task<StockItemDto> Handle(CreateStockItemCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var sku = request.Sku.Trim().ToUpperInvariant();
        if (await stockItems.GetAsync(item => item.Sku == sku) is not null)
            throw new InvalidOperationException("A stock item with that SKU already exists.");
        var entity = new StockItem(Guid.NewGuid(), request.CatalogItemId, request.Sku, request.Description,
            request.UnitSymbol, request.StorageLocation, request.ReorderLevel);
        await stockItems.AddAsync(entity);
        return StockMapping.ToDto(entity);
    }
}

public sealed class RecordStockMovementCommandHandler(
    IRepositoryBase<StockItem> stockItems,
    IRepositoryBase<StockMovement> movements,
    IProcurementUnitOfWork unitOfWork)
    : IRequestHandler<RecordStockMovementCommand, StockMovementDto>
{
    public Task<StockMovementDto> Handle(RecordStockMovementCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var matches = await stockItems.GetAsync(predicate: x => x.Id == request.StockItemId,
                includes: new(), disableTracking: false);
            var item = matches.FirstOrDefault();
            if (item is null) throw new KeyNotFoundException("Stock item was not found.");

            var signedQuantity = request.MovementType switch
            {
                StockMovementType.Receipt or StockMovementType.Return or StockMovementType.TransferIn or StockMovementType.Adjustment => request.Quantity,
                StockMovementType.Consumption or StockMovementType.TransferOut => -request.Quantity,
                _ => throw new ArgumentOutOfRangeException(nameof(request.MovementType))
            };
            item.ApplyMovement(signedQuantity);
            var movement = new StockMovement(Guid.NewGuid(), item.Id, request.MovementType, request.Quantity,
                request.SourceDocument, request.ProjectId, request.Notes);
            await stockItems.UpdateAsync(item);
            await movements.AddAsync(movement);
            return StockMapping.ToDto(movement);
        }, cancellationToken);
}

public sealed class GetStockMovementsQueryHandler(
    IRepositoryBase<StockItem> stockItems,
    IRepositoryBase<StockMovement> movements)
    : IRequestHandler<GetStockMovementsQuery, PagedResult<StockMovementDto>>
{
    public async Task<PagedResult<StockMovementDto>> Handle(GetStockMovementsQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (await stockItems.GetByIdAsync(request.StockItemId) is null)
            throw new KeyNotFoundException("Stock item was not found.");
        var page = await movements.GetPagedAsync(request.Page, request.PageSize,
            predicate: movement => movement.StockItemId == request.StockItemId,
            orderBy: query => query.OrderByDescending(movement => movement.CreatedDate).ThenByDescending(movement => movement.Id));
        return new PagedResult<StockMovementDto>(page.Items.Select(StockMapping.ToDto).ToArray(),
            page.Page, page.PageSize, page.TotalCount);
    }
}