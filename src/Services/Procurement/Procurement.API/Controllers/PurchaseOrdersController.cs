using EventBus.Messages.Events;
using Shared.ServiceKit.Outbox;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Procurement.Application.DTOs;
using Procurement.Application.Features.PurchaseOrders;
using Procurement.Application.Features.Suppliers;
using Procurement.Domain.Enums;

namespace Procurement.API.Controllers;

[ApiController]
[Route("api/v1/purchase-orders")]
[Authorize]
public sealed class PurchaseOrdersController(
    IMediator mediator,
    IIntegrationOutbox outbox) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "procurement.read")]
    public async Task<ActionResult<PagedResult<PurchaseOrderDto>>> Get(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] PurchaseOrderStatus? status = null, CancellationToken cancellationToken = default) =>
        Ok(await mediator.Send(new GetPurchaseOrdersQuery(page, pageSize, status), cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "procurement.read")]
    public async Task<ActionResult<PurchaseOrderDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var order = await mediator.Send(new GetPurchaseOrderByIdQuery(id), cancellationToken);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPost]
    [Authorize(Policy = "procurement.write")]
    [ProducesResponseType(typeof(PurchaseOrderDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<PurchaseOrderDto>> Create(
        [FromBody] CreatePurchaseOrderCommand command, CancellationToken cancellationToken)
    {
        var order = await mediator.Send(command, cancellationToken);
        var supplier = await mediator.Send(new GetSupplierByIdQuery(order.SupplierId), cancellationToken);
        outbox.Enqueue(new PurchaseOrderCreatedEvent
        {
            PurchaseOrderId = order.Id,
            SupplierId = order.SupplierId,
            SupplierName = supplier?.Name ?? string.Empty,
            OrderNumber = order.OrderNumber,
            TotalAmount = order.OrderTotal,
            Currency = supplier?.CurrencyCode ?? "BYN",
            OrderDate = ToUtcDateTime(order.OrderDate),
            ExpectedDeliveryDate = order.ExpectedDeliveryDate is null ? null : ToUtcDateTime(order.ExpectedDeliveryDate.Value)
        });
        return CreatedAtAction(nameof(GetById), new { id = order.Id }, order);
    }

    [HttpPost("{id:guid}/submit")]
    [Authorize(Policy = "procurement.write")]
    public async Task<ActionResult<PurchaseOrderDto>> Submit(Guid id, CancellationToken cancellationToken)
    {
        var order = await mediator.Send(new SubmitPurchaseOrderCommand(id), cancellationToken);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = "procurement.write")]
    public async Task<ActionResult<PurchaseOrderDto>> Approve(
        Guid id, [FromBody] ApprovePurchaseOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await mediator.Send(new ApprovePurchaseOrderCommand(id, request.ApprovalDate), cancellationToken);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPost("{id:guid}/receive-lines/{lineId:guid}")]
    [Authorize(Policy = "procurement.write")]
    public async Task<ActionResult<PurchaseOrderDto>> ReceiveLine(
        Guid id, Guid lineId, [FromBody] ReceivePurchaseOrderLineRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ReceivePurchaseOrderLineCommand(id, lineId,
            request.Quantity, request.ReceivedDate), cancellationToken);
        if (result is null) return NotFound();

        var line = result.ReceivedLine;
        if (result.StockMovementId.HasValue && line.ItemType == ProcurementItemType.Material && line.CatalogItemId.HasValue)
        {
            outbox.Enqueue(new MaterialReceivedEvent
            {
                MaterialReceiptId = result.StockMovementId.Value,
                PurchaseOrderId = id,
                MaterialId = line.CatalogItemId.Value,
                MaterialName = line.Description,
                UnitSymbol = line.UnitSymbol,
                QuantityReceived = request.Quantity,
                UnitCost = line.UnitPrice,
                TotalCost = decimal.Round(request.Quantity * line.UnitPrice, 2, MidpointRounding.AwayFromZero),
                ReceivedDate = ToUtcDateTime(request.ReceivedDate),
                ReceivedBy = User.Identity?.Name ?? string.Empty
            });
        }
        return Ok(result.Order);
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = "procurement.write")]
    public async Task<ActionResult<PurchaseOrderDto>> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var order = await mediator.Send(new CancelPurchaseOrderCommand(id), cancellationToken);
        return order is null ? NotFound() : Ok(order);
    }

    private static DateTime ToUtcDateTime(DateOnly date) =>
        DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
}

public sealed record ApprovePurchaseOrderRequest(DateOnly ApprovalDate);
public sealed record ReceivePurchaseOrderLineRequest(decimal Quantity, DateOnly ReceivedDate);