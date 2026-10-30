using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Procurement.Application.DTOs;
using Procurement.Application.Features.Suppliers;
using Shared.Common.Persistence;

namespace Procurement.API.Controllers;

[ApiController]
[Route("api/v1/suppliers")]
[Authorize]
public sealed class SuppliersController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "procurement.read")]
    public async Task<ActionResult<PagedResult<SupplierDto>>> Get(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] bool includeInactive = false, CancellationToken cancellationToken = default) =>
        Ok(await mediator.Send(new GetSuppliersQuery(page, pageSize, includeInactive), cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "procurement.read")]
    public async Task<ActionResult<SupplierDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var supplier = await mediator.Send(new GetSupplierByIdQuery(id), cancellationToken);
        return supplier is null ? NotFound() : Ok(supplier);
    }

    [HttpPost]
    [Authorize(Policy = "procurement.write")]
    [ProducesResponseType(typeof(SupplierDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<SupplierDto>> Create(
        [FromBody] SupplierRequest request, CancellationToken cancellationToken)
    {
        var supplier = await mediator.Send(new CreateSupplierCommand(request.Code, request.Name,
            request.ContactName, request.Email, request.Phone, request.TaxIdentifier, request.Address,
            request.PaymentTermDays, request.CurrencyCode), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = supplier.Id }, supplier);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "procurement.write")]
    public async Task<ActionResult<SupplierDto>> Update(
        Guid id, [FromBody] SupplierRequest request, CancellationToken cancellationToken)
    {
        var supplier = await mediator.Send(new UpdateSupplierCommand(id, request.Code, request.Name,
            request.ContactName, request.Email, request.Phone, request.TaxIdentifier, request.Address,
            request.PaymentTermDays, request.CurrencyCode), cancellationToken);
        return supplier is null ? NotFound() : Ok(supplier);
    }

    [HttpPatch("{id:guid}/active")]
    [Authorize(Policy = "procurement.write")]
    public async Task<IActionResult> SetActive(
        Guid id, [FromBody] SetSupplierActiveRequest request, CancellationToken cancellationToken)
    {
        var updated = await mediator.Send(new SetSupplierActiveCommand(id, request.IsActive), cancellationToken);
        return updated ? NoContent() : NotFound();
    }
}

public sealed record SupplierRequest(
    string Code, string Name, string ContactName, string? Email, string? Phone,
    string? TaxIdentifier, string Address, int PaymentTermDays, string CurrencyCode = "BYN");
public sealed record SetSupplierActiveRequest(bool IsActive);