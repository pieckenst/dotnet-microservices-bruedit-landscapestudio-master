using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procurement.Domain.Entities;
using Procurement.Infrastructure.Persistence;
using Shared.Common.Persistence;

namespace Procurement.API.Controllers;

[ApiController]
[Route("api/v1/suppliers")]
[Authorize]
public sealed class SuppliersController(ProcurementContext context) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "procurement.read")]
    public async Task<ActionResult<PagedResult<Supplier>>> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        ValidatePage(page, pageSize);
        var query = context.Suppliers.AsNoTracking();
        if (!includeInactive) query = query.Where(supplier => supplier.IsActive);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(supplier => supplier.Name)
            .ThenBy(supplier => supplier.Code)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);
        return Ok(new PagedResult<Supplier>(items, page, pageSize, total));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "procurement.read")]
    public async Task<ActionResult<Supplier>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var supplier = await context.Suppliers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return supplier is null ? NotFound() : Ok(supplier);
    }

    [HttpPost]
    [Authorize(Policy = "procurement.write")]
    public async Task<ActionResult<Supplier>> Create([FromBody] SupplierRequest request, CancellationToken cancellationToken)
    {
        var supplier = new Supplier(Guid.NewGuid(), request.Code, request.Name, request.ContactName,
            request.Email, request.Phone, request.TaxIdentifier, request.Address,
            request.PaymentTermDays, request.CurrencyCode);
        await context.Suppliers.AddAsync(supplier, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = supplier.Id }, supplier);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "procurement.write")]
    public async Task<ActionResult<Supplier>> Update(Guid id, [FromBody] SupplierRequest request, CancellationToken cancellationToken)
    {
        var supplier = await context.Suppliers.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (supplier is null) return NotFound();
        supplier.Update(request.Code, request.Name, request.ContactName, request.Email, request.Phone,
            request.TaxIdentifier, request.Address, request.PaymentTermDays, request.CurrencyCode);
        await context.SaveChangesAsync(cancellationToken);
        return Ok(supplier);
    }

    [HttpPatch("{id:guid}/active")]
    [Authorize(Policy = "procurement.write")]
    public async Task<IActionResult> SetActive(Guid id, [FromBody] SetSupplierActiveRequest request, CancellationToken cancellationToken)
    {
        var supplier = await context.Suppliers.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (supplier is null) return NotFound();
        supplier.SetActive(request.IsActive);
        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page), "Page must be greater than zero.");
        if (pageSize is < 1 or > 200) throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be between 1 and 200.");
    }
}

public sealed record SupplierRequest(
    string Code, string Name, string ContactName, string? Email, string? Phone,
    string? TaxIdentifier, string Address, int PaymentTermDays, string CurrencyCode = "BYN");
public sealed record SetSupplierActiveRequest(bool IsActive);