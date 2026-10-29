using EventBus.Messages.Events;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procurement.Domain.Entities;
using Procurement.Domain.Enums;
using Procurement.Infrastructure.Persistence;
using Shared.Common.Persistence;

namespace Procurement.API.Controllers;

[ApiController]
[Route("api/v1/purchase-orders")]
[Authorize]
public sealed class PurchaseOrdersController(ProcurementContext context, IPublishEndpoint publishEndpoint, ILogger<PurchaseOrdersController> logger) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "procurement.read")]
    public async Task<ActionResult<PagedResult<PurchaseOrderDto>>> Get(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] PurchaseOrderStatus? status = null, CancellationToken cancellationToken = default)
    {
        ValidatePage(page, pageSize);
        var query = context.PurchaseOrders.AsNoTracking().Include(order => order.Lines).AsQueryable();
        if (status is not null) query = query.Where(order => order.Status == status);
        var total = await query.CountAsync(cancellationToken);
        var orders = await query.OrderByDescending(order => order.OrderDate)
            .ThenBy(order => order.OrderNumber).Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);
        return Ok(new PagedResult<PurchaseOrderDto>(orders.Select(ToDto).ToList(), page, pageSize, total));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "procurement.read")]
    public async Task<ActionResult<PurchaseOrderDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var order = await context.PurchaseOrders.AsNoTracking().Include(x => x.Lines)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return order is null ? NotFound() : Ok(ToDto(order));
    }

    [HttpPost]
    [Authorize(Policy = "procurement.write")]
    public async Task<ActionResult<PurchaseOrderDto>> Create(
        [FromBody] CreatePurchaseOrderRequest request, CancellationToken cancellationToken)
    {
        var supplier = await context.Suppliers.AsNoTracking().SingleOrDefaultAsync(
            supplier => supplier.Id == request.SupplierId && supplier.IsActive, cancellationToken);
        if (supplier is null) return BadRequest(new ProblemDetails
        {
            Title = "Invalid supplier",
            Detail = "The purchase order must reference an existing active supplier.",
            Status = StatusCodes.Status400BadRequest
        });

        var order = new PurchaseOrder(Guid.NewGuid(), request.OrderNumber, request.SupplierId,
            request.ProjectId, request.OrderDate, request.ExpectedDeliveryDate, request.Notes ?? string.Empty);
        foreach (var lineRequest in request.Lines ?? [])
        {
            order.AddLine(new PurchaseOrderLine(Guid.NewGuid(), order.Id, lineRequest.ItemType,
                lineRequest.CatalogItemId, lineRequest.Description, lineRequest.UnitSymbol,
                lineRequest.Quantity, lineRequest.UnitPrice));
        }
        await context.PurchaseOrders.AddAsync(order, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        try
        {
            await publishEndpoint.Publish(new PurchaseOrderCreatedEvent
            {
                PurchaseOrderId = order.Id,
                SupplierId = order.SupplierId,
                // The analytics stream intentionally receives identifiers, not supplier contact names.
                SupplierName = string.Empty,
                OrderNumber = order.OrderNumber,
                TotalAmount = order.OrderTotal,
                Currency = supplier.CurrencyCode,
                OrderDate = ToUtcDateTime(order.OrderDate),
                ExpectedDeliveryDate = order.ExpectedDeliveryDate is null ? null : ToUtcDateTime(order.ExpectedDeliveryDate.Value)
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Purchase order {OrderId} was saved but its analytics event could not be published.", order.Id);
        }

        return CreatedAtAction(nameof(GetById), new { id = order.Id }, ToDto(order));
    }

    [HttpPost("{id:guid}/submit")]
    [Authorize(Policy = "procurement.write")]
    public async Task<ActionResult<PurchaseOrderDto>> Submit(Guid id, CancellationToken cancellationToken)
    {
        var order = await LoadTracked(id, cancellationToken);
        if (order is null) return NotFound();
        order.Submit();
        await context.SaveChangesAsync(cancellationToken);
        return Ok(ToDto(order));
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = "procurement.write")]
    public async Task<ActionResult<PurchaseOrderDto>> Approve(
        Guid id, [FromBody] ApprovePurchaseOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await LoadTracked(id, cancellationToken);
        if (order is null) return NotFound();
        order.Approve(request.ApprovalDate);
        await context.SaveChangesAsync(cancellationToken);
        return Ok(ToDto(order));
    }

    [HttpPost("{id:guid}/receive-lines/{lineId:guid}")]
    [Authorize(Policy = "procurement.write")]
    public async Task<ActionResult<PurchaseOrderDto>> ReceiveLine(
        Guid id, Guid lineId, [FromBody] ReceivePurchaseOrderLineRequest request, CancellationToken cancellationToken)
    {
        var order = await LoadTracked(id, cancellationToken);
        if (order is null) return NotFound();

        var line = order.Lines.SingleOrDefault(x => x.Id == lineId);
        if (line is null) return NotFound();

        // Persist the order state and inventory ledger together. A missing stock mapping
        // is rejected before mutating the aggregate, so the caller can configure it first.
        StockItem? stockItem = null;
        if (line.CatalogItemId is not null && line.ItemType != ProcurementItemType.Service)
        {
            stockItem = await context.StockItems.SingleOrDefaultAsync(
                item => item.CatalogItemId == line.CatalogItemId, cancellationToken);
            if (stockItem is null)
                return Conflict(new ProblemDetails
                {
                    Title = "Stock mapping required",
                    Detail = "Create a stock item linked to this catalog item before receiving the purchase-order line.",
                    Status = StatusCodes.Status409Conflict
                });
        }

        var receivedLine = order.ReceiveLine(lineId, request.Quantity, request.ReceivedDate);
        Guid? movementId = null;
        if (stockItem is not null)
        {
            stockItem.ApplyMovement(request.Quantity);
            movementId = Guid.NewGuid();
            await context.StockMovements.AddAsync(new StockMovement(movementId.Value, stockItem.Id,
                StockMovementType.Receipt, request.Quantity, order.OrderNumber, order.ProjectId,
                $"Receipt for purchase-order line {receivedLine.Id}"), cancellationToken);
        }
        await context.SaveChangesAsync(cancellationToken);

        if (movementId.HasValue && receivedLine.ItemType == ProcurementItemType.Material && receivedLine.CatalogItemId.HasValue)
        {
            try
            {
                await publishEndpoint.Publish(new MaterialReceivedEvent
                {
                    MaterialReceiptId = movementId.Value,
                    PurchaseOrderId = order.Id,
                    MaterialId = receivedLine.CatalogItemId.Value,
                    MaterialName = string.Empty,
                    UnitSymbol = receivedLine.UnitSymbol,
                    QuantityReceived = request.Quantity,
                    UnitCost = receivedLine.UnitPrice,
                    TotalCost = decimal.Round(request.Quantity * receivedLine.UnitPrice, 2, MidpointRounding.AwayFromZero),
                    ReceivedDate = ToUtcDateTime(request.ReceivedDate),
                    ReceivedBy = string.Empty
                }, cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Inventory receipt {MovementId} was saved but its analytics event could not be published.", movementId.Value);
            }
        }
        return Ok(ToDto(order));
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = "procurement.write")]
    public async Task<ActionResult<PurchaseOrderDto>> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var order = await LoadTracked(id, cancellationToken);
        if (order is null) return NotFound();
        order.Cancel();
        await context.SaveChangesAsync(cancellationToken);
        return Ok(ToDto(order));
    }

    private Task<PurchaseOrder?> LoadTracked(Guid id, CancellationToken cancellationToken) =>
        context.PurchaseOrders.Include(order => order.Lines)
            .SingleOrDefaultAsync(order => order.Id == id, cancellationToken);

    private static DateTime ToUtcDateTime(DateOnly date) =>
        DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);

    private static PurchaseOrderDto ToDto(PurchaseOrder order) => new(
        order.Id, order.OrderNumber, order.SupplierId, order.ProjectId, order.OrderDate,
        order.ExpectedDeliveryDate, order.ApprovedDate, order.CompletedDate, order.Status,
        order.Notes, order.OrderTotal, order.Lines.OrderBy(line => line.Description)
            .Select(line => new PurchaseOrderLineDto(line.Id, line.ItemType, line.CatalogItemId,
                line.Description, line.UnitSymbol, line.Quantity, line.ReceivedQuantity, line.UnitPrice, line.LineTotal))
            .ToArray());

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page), "Page must be greater than zero.");
        if (pageSize is < 1 or > 200) throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be between 1 and 200.");
    }
}

public sealed record CreatePurchaseOrderRequest(
    string OrderNumber, Guid SupplierId, Guid? ProjectId, DateOnly OrderDate,
    DateOnly? ExpectedDeliveryDate, string? Notes, IReadOnlyList<CreatePurchaseOrderLineRequest>? Lines);
public sealed record CreatePurchaseOrderLineRequest(
    ProcurementItemType ItemType, Guid? CatalogItemId, string Description,
    string UnitSymbol, decimal Quantity, decimal UnitPrice);
public sealed record ApprovePurchaseOrderRequest(DateOnly ApprovalDate);
public sealed record ReceivePurchaseOrderLineRequest(decimal Quantity, DateOnly ReceivedDate);
public sealed record PurchaseOrderLineDto(Guid Id, ProcurementItemType ItemType, Guid? CatalogItemId,
    string Description, string UnitSymbol, decimal Quantity, decimal ReceivedQuantity, decimal UnitPrice, decimal LineTotal);
public sealed record PurchaseOrderDto(Guid Id, string OrderNumber, Guid SupplierId, Guid? ProjectId,
    DateOnly OrderDate, DateOnly? ExpectedDeliveryDate, DateOnly? ApprovedDate, DateOnly? CompletedDate,
    PurchaseOrderStatus Status, string Notes, decimal OrderTotal, IReadOnlyList<PurchaseOrderLineDto> Lines);