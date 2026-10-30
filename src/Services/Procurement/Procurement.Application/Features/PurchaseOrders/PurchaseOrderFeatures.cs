using FluentValidation;
using MediatR;
using Procurement.Application.Contracts;
using Procurement.Application.DTOs;
using Procurement.Domain.Entities;
using Procurement.Domain.Enums;
using Shared.Common.Persistence;

namespace Procurement.Application.Features.PurchaseOrders;

public sealed record CreatePurchaseOrderLineInput(ProcurementItemType ItemType, Guid? CatalogItemId,
    string Description, string UnitSymbol, decimal Quantity, decimal UnitPrice);
public sealed record GetPurchaseOrdersQuery(int Page = 1, int PageSize = 50, PurchaseOrderStatus? Status = null)
    : IRequest<PagedResult<PurchaseOrderDto>>;
public sealed record GetPurchaseOrderByIdQuery(Guid Id) : IRequest<PurchaseOrderDto?>;
public sealed record CreatePurchaseOrderCommand(string OrderNumber, Guid SupplierId, Guid? ProjectId,
    DateOnly OrderDate, DateOnly? ExpectedDeliveryDate, string? Notes,
    IReadOnlyList<CreatePurchaseOrderLineInput>? Lines) : IRequest<PurchaseOrderDto>;
public sealed record SubmitPurchaseOrderCommand(Guid Id) : IRequest<PurchaseOrderDto?>;
public sealed record ApprovePurchaseOrderCommand(Guid Id, DateOnly ApprovalDate) : IRequest<PurchaseOrderDto?>;
public sealed record ReceivePurchaseOrderLineCommand(Guid Id, Guid LineId, decimal Quantity, DateOnly ReceivedDate)
    : IRequest<PurchaseOrderDto?>;
public sealed record CancelPurchaseOrderCommand(Guid Id) : IRequest<PurchaseOrderDto?>;

public sealed class GetPurchaseOrdersQueryValidator : AbstractValidator<GetPurchaseOrdersQuery>
{
    public GetPurchaseOrdersQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.Status).Must(status => !status.HasValue || Enum.IsDefined(status.Value));
    }
}
public sealed class GetPurchaseOrderByIdQueryValidator : AbstractValidator<GetPurchaseOrderByIdQuery>
{
    public GetPurchaseOrderByIdQueryValidator() => RuleFor(x => x.Id).NotEmpty();
}
public sealed class CreatePurchaseOrderCommandValidator : AbstractValidator<CreatePurchaseOrderCommand>
{
    public CreatePurchaseOrderCommandValidator()
    {
        RuleFor(x => x.OrderNumber).NotEmpty().MaximumLength(40);
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.ProjectId).Must(id => !id.HasValue || id.Value != Guid.Empty);
        RuleFor(x => x.ExpectedDeliveryDate).Must((command, date) => !date.HasValue || date.Value >= command.OrderDate);
        RuleFor(x => x.Notes).MaximumLength(4000);
        RuleFor(x => x.Lines).NotNull().NotEmpty().Must(lines => lines is null || lines.Count <= 250);
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(x => x.ItemType).IsInEnum();
            line.RuleFor(x => x.CatalogItemId).Must(id => !id.HasValue || id.Value != Guid.Empty);
            line.RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
            line.RuleFor(x => x.UnitSymbol).NotEmpty().MaximumLength(24);
            line.RuleFor(x => x.Quantity).GreaterThan(0).LessThanOrEqualTo(1_000_000_000m);
            line.RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0).LessThanOrEqualTo(1_000_000_000_000m);
        });
    }
}
public sealed class SubmitPurchaseOrderCommandValidator : AbstractValidator<SubmitPurchaseOrderCommand>
{
    public SubmitPurchaseOrderCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}
public sealed class ApprovePurchaseOrderCommandValidator : AbstractValidator<ApprovePurchaseOrderCommand>
{
    public ApprovePurchaseOrderCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}
public sealed class ReceivePurchaseOrderLineCommandValidator : AbstractValidator<ReceivePurchaseOrderLineCommand>
{
    public ReceivePurchaseOrderLineCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.LineId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0).LessThanOrEqualTo(1_000_000_000m);
    }
}
public sealed class CancelPurchaseOrderCommandValidator : AbstractValidator<CancelPurchaseOrderCommand>
{
    public CancelPurchaseOrderCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}

public static class PurchaseOrderMapping
{
    public static PurchaseOrderDto ToDto(PurchaseOrder x) => new(x.Id, x.OrderNumber, x.SupplierId, x.ProjectId,
        x.OrderDate, x.ExpectedDeliveryDate, x.ApprovedDate, x.CompletedDate, x.Status, x.Notes, x.OrderTotal,
        x.Lines.OrderBy(line => line.Description).ThenBy(line => line.Id).Select(line =>
            new PurchaseOrderLineDto(line.Id, line.ItemType, line.CatalogItemId, line.Description,
                line.UnitSymbol, line.Quantity, line.ReceivedQuantity, line.UnitPrice, line.LineTotal)).ToArray(),
        x.CreatedDate, x.LastModifiedDate);
}

public sealed class GetPurchaseOrdersQueryHandler(IRepositoryBase<PurchaseOrder> orders)
    : IRequestHandler<GetPurchaseOrdersQuery, PagedResult<PurchaseOrderDto>>
{
    public async Task<PagedResult<PurchaseOrderDto>> Handle(GetPurchaseOrdersQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var page = await orders.GetPagedAsync(request.Page, request.PageSize,
            predicate: order => !request.Status.HasValue || order.Status == request.Status,
            orderBy: query => query.OrderByDescending(order => order.OrderDate).ThenBy(order => order.OrderNumber),
            includes: [order => order.Lines]);
        return new PagedResult<PurchaseOrderDto>(page.Items.Select(PurchaseOrderMapping.ToDto).ToArray(),
            page.Page, page.PageSize, page.TotalCount);
    }
}

public sealed class GetPurchaseOrderByIdQueryHandler(IRepositoryBase<PurchaseOrder> orders)
    : IRequestHandler<GetPurchaseOrderByIdQuery, PurchaseOrderDto?>
{
    public async Task<PurchaseOrderDto?> Handle(GetPurchaseOrderByIdQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var order = await orders.GetAsync(predicate: item => item.Id == request.Id,
            includes: [item => item.Lines], disableTracking: true);
        var entity = order.FirstOrDefault();
        return entity is null ? null : PurchaseOrderMapping.ToDto(entity);
    }
}

public sealed class CreatePurchaseOrderCommandHandler(
    IRepositoryBase<PurchaseOrder> orders,
    IRepositoryBase<Supplier> suppliers)
    : IRequestHandler<CreatePurchaseOrderCommand, PurchaseOrderDto>
{
    public async Task<PurchaseOrderDto> Handle(CreatePurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var supplier = await suppliers.GetByIdAsync(request.SupplierId);
        if (supplier is null || !supplier.IsActive)
            throw new InvalidOperationException("The purchase order must reference an existing active supplier.");
        var number = request.OrderNumber.Trim().ToUpperInvariant();
        if (await orders.GetAsync(item => item.OrderNumber == number) is not null)
            throw new InvalidOperationException("A purchase order with that number already exists.");

        var entity = new PurchaseOrder(Guid.NewGuid(), request.OrderNumber, request.SupplierId,
            request.ProjectId, request.OrderDate, request.ExpectedDeliveryDate, request.Notes ?? string.Empty);
        foreach (var line in request.Lines ?? [])
            entity.AddLine(new PurchaseOrderLine(Guid.NewGuid(), entity.Id, line.ItemType,
                line.CatalogItemId, line.Description, line.UnitSymbol, line.Quantity, line.UnitPrice));
        await orders.AddAsync(entity);
        return PurchaseOrderMapping.ToDto(entity);
    }
}

public sealed class SubmitPurchaseOrderCommandHandler(IRepositoryBase<PurchaseOrder> orders)
    : IRequestHandler<SubmitPurchaseOrderCommand, PurchaseOrderDto?>
{
    public async Task<PurchaseOrderDto?> Handle(SubmitPurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entity = await orders.GetAsync(predicate: x => x.Id == request.Id,
            includes: [x => x.Lines], disableTracking: false);
        var order = entity.FirstOrDefault();
        if (order is null) return null;
        order.Submit();
        await orders.UpdateAsync(order);
        return PurchaseOrderMapping.ToDto(order);
    }
}

public sealed class ApprovePurchaseOrderCommandHandler(IRepositoryBase<PurchaseOrder> orders)
    : IRequestHandler<ApprovePurchaseOrderCommand, PurchaseOrderDto?>
{
    public async Task<PurchaseOrderDto?> Handle(ApprovePurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entities = await orders.GetAsync(predicate: x => x.Id == request.Id,
            includes: [x => x.Lines], disableTracking: false);
        var order = entities.FirstOrDefault();
        if (order is null) return null;
        order.Approve(request.ApprovalDate);
        await orders.UpdateAsync(order);
        return PurchaseOrderMapping.ToDto(order);
    }
}

public sealed class ReceivePurchaseOrderLineCommandHandler(
    IRepositoryBase<PurchaseOrder> orders,
    IRepositoryBase<StockItem> stockItems,
    IRepositoryBase<StockMovement> movements,
    IProcurementUnitOfWork unitOfWork)
    : IRequestHandler<ReceivePurchaseOrderLineCommand, PurchaseOrderDto?>
{
    public Task<PurchaseOrderDto?> Handle(ReceivePurchaseOrderLineCommand request, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var results = await orders.GetAsync(predicate: x => x.Id == request.Id,
                includes: [x => x.Lines], disableTracking: false);
            var order = results.FirstOrDefault();
            if (order is null) return null;

            var line = order.Lines.SingleOrDefault(item => item.Id == request.LineId);
            if (line is null) throw new KeyNotFoundException("Purchase-order line was not found.");

            StockItem? stock = null;
            if (line.CatalogItemId.HasValue && line.ItemType != ProcurementItemType.Service)
            {
                var stockResults = await stockItems.GetAsync(predicate: item => item.CatalogItemId == line.CatalogItemId,
                    includes: new(), disableTracking: false);
                stock = stockResults.FirstOrDefault();
                if (stock is null)
                    throw new InvalidOperationException("Create a stock item linked to this catalog item before receiving the purchase-order line.");
            }

            var received = order.ReceiveLine(request.LineId, request.Quantity, request.ReceivedDate);
            if (stock is not null)
            {
                stock.ApplyMovement(request.Quantity);
                await stockItems.UpdateAsync(stock);
                await movements.AddAsync(new StockMovement(Guid.NewGuid(), stock.Id, StockMovementType.Receipt,
                    request.Quantity, order.OrderNumber, order.ProjectId,
                    $"Receipt for purchase-order line {received.Id}"));
            }
            await orders.UpdateAsync(order);
            return PurchaseOrderMapping.ToDto(order);
        }, cancellationToken);
}

public sealed class CancelPurchaseOrderCommandHandler(IRepositoryBase<PurchaseOrder> orders)
    : IRequestHandler<CancelPurchaseOrderCommand, PurchaseOrderDto?>
{
    public async Task<PurchaseOrderDto?> Handle(CancelPurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var results = await orders.GetAsync(predicate: x => x.Id == request.Id,
            includes: [x => x.Lines], disableTracking: false);
        var order = results.FirstOrDefault();
        if (order is null) return null;
        order.Cancel();
        await orders.UpdateAsync(order);
        return PurchaseOrderMapping.ToDto(order);
    }
}