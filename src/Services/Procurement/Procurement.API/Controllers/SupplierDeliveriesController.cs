using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Procurement.Application.DTOs;
using Procurement.Application.Features.Deliveries;
using Procurement.Domain.Enums;
using Shared.Common.Persistence;

namespace Procurement.API.Controllers;

[ApiController]
[Route("api/v1/deliveries")]
[Authorize]
public sealed class SupplierDeliveriesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "procurement.read")]
    public async Task<ActionResult<PagedResult<SupplierDeliveryDto>>> Get(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] Guid? purchaseOrderId = null, [FromQuery] DeliveryStatus? status = null,
        CancellationToken cancellationToken = default) =>
        Ok(await mediator.Send(new GetSupplierDeliveriesQuery(page, pageSize, purchaseOrderId, status), cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "procurement.read")]
    public async Task<ActionResult<SupplierDeliveryDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var delivery = await mediator.Send(new GetSupplierDeliveryByIdQuery(id), cancellationToken);
        return delivery is null ? NotFound() : Ok(delivery);
    }

    [HttpPost]
    [Authorize(Policy = "procurement.write")]
    public async Task<ActionResult<SupplierDeliveryDto>> Create(
        [FromBody] CreateSupplierDeliveryRequest request, CancellationToken cancellationToken)
    {
        var delivery = await mediator.Send(new CreateSupplierDeliveryCommand(request.PurchaseOrderId,
            request.DeliveryNumber, request.ExpectedDate, request.Carrier, request.TrackingReference), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = delivery.Id }, delivery);
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = "procurement.write")]
    public async Task<ActionResult<SupplierDeliveryDto>> ChangeStatus(
        Guid id, [FromBody] ChangeSupplierDeliveryStatusRequest request, CancellationToken cancellationToken)
    {
        var delivery = await mediator.Send(new ChangeSupplierDeliveryStatusCommand(id, request.Status, request.ActualDate), cancellationToken);
        return delivery is null ? NotFound() : Ok(delivery);
    }
}

public sealed record CreateSupplierDeliveryRequest(Guid PurchaseOrderId, string DeliveryNumber,
    DateOnly ExpectedDate, string Carrier, string? TrackingReference = null);
public sealed record ChangeSupplierDeliveryStatusRequest(DeliveryStatus Status, DateOnly? ActualDate = null);