using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procurement.Domain.Entities;
using Procurement.Domain.Enums;
using Procurement.Infrastructure.Persistence;
using Shared.Common.Persistence;

namespace Procurement.API.Controllers;

[ApiController]
[Route("api/v1/deliveries")]
[Authorize]
public sealed class SupplierDeliveriesController(ProcurementContext context) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "procurement.read")]
    public async Task<ActionResult<PagedResult<SupplierDeliveryDto>>> Get(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] Guid? purchaseOrderId = null, [FromQuery] DeliveryStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        ValidatePage(page, pageSize);
        var query = context.Deliveries.AsNoTracking().AsQueryable();
        if (purchaseOrderId is not null) query = query.Where(delivery => delivery.PurchaseOrderId == purchaseOrderId);
        if (status is not null) query = query.Where(delivery => delivery.Status == status);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(delivery => delivery.ExpectedDate)
            .ThenBy(delivery => delivery.DeliveryNumber)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(delivery => new SupplierDeliveryDto(delivery.Id, delivery.PurchaseOrderId,
                delivery.DeliveryNumber, delivery.ExpectedDate, delivery.ActualDate,
                delivery.Carrier, delivery.TrackingReference, delivery.Status))
            .ToListAsync(cancellationToken);
        return Ok(new PagedResult<SupplierDeliveryDto>(items, page, pageSize, total));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "procurement.read")]
    public async Task<ActionResult<SupplierDeliveryDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var delivery = await context.Deliveries.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return delivery is null ? NotFound() : Ok(ToDto(delivery));
    }

    [HttpPost]
    [Authorize(Policy = "procurement.write")]
    public async Task<ActionResult<SupplierDeliveryDto>> Create(
        [FromBody] CreateSupplierDeliveryRequest request, CancellationToken cancellationToken)
    {
        var order = await context.PurchaseOrders.AsNoTracking()
            .SingleOrDefaultAsync(order => order.Id == request.PurchaseOrderId, cancellationToken);
        if (order is null) return BadRequest(new ProblemDetails
        {
            Title = "Invalid purchase order",
            Detail = "The delivery must reference an existing purchase order.",
            Status = StatusCodes.Status400BadRequest
        });
        if (order.Status is not (PurchaseOrderStatus.Approved or PurchaseOrderStatus.PartiallyReceived))
            return Conflict(new ProblemDetails
            {
                Title = "Purchase order is not receivable",
                Detail = "A delivery can only be scheduled for an approved or partially received purchase order.",
                Status = StatusCodes.Status409Conflict
            });

        var delivery = new SupplierDelivery(Guid.NewGuid(), request.PurchaseOrderId,
            request.DeliveryNumber, request.ExpectedDate, request.Carrier, request.TrackingReference ?? string.Empty);
        await context.Deliveries.AddAsync(delivery, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = delivery.Id }, ToDto(delivery));
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = "procurement.write")]
    public async Task<ActionResult<SupplierDeliveryDto>> ChangeStatus(
        Guid id, [FromBody] ChangeSupplierDeliveryStatusRequest request, CancellationToken cancellationToken)
    {
        var delivery = await context.Deliveries.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (delivery is null) return NotFound();
        delivery.ChangeStatus(request.Status, request.ActualDate);
        await context.SaveChangesAsync(cancellationToken);
        return Ok(ToDto(delivery));
    }

    private static SupplierDeliveryDto ToDto(SupplierDelivery delivery) =>
        new(delivery.Id, delivery.PurchaseOrderId, delivery.DeliveryNumber, delivery.ExpectedDate,
            delivery.ActualDate, delivery.Carrier, delivery.TrackingReference, delivery.Status);

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page), "Page must be greater than zero.");
        if (pageSize is < 1 or > 200) throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be between 1 and 200.");
    }
}

public sealed record CreateSupplierDeliveryRequest(Guid PurchaseOrderId, string DeliveryNumber,
    DateOnly ExpectedDate, string Carrier, string? TrackingReference = null);
public sealed record ChangeSupplierDeliveryStatusRequest(DeliveryStatus Status, DateOnly? ActualDate = null);
public sealed record SupplierDeliveryDto(Guid Id, Guid PurchaseOrderId, string DeliveryNumber,
    DateOnly ExpectedDate, DateOnly? ActualDate, string Carrier, string TrackingReference, DeliveryStatus Status);