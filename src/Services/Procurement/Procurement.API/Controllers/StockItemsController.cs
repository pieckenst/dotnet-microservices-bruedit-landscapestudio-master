using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procurement.Domain.Entities;
using Procurement.Domain.Enums;
using Procurement.Infrastructure.Persistence;
using Shared.Common.Persistence;

namespace Procurement.API.Controllers;

[ApiController]
[Route("api/v1/stock-items")]
[Authorize]
public sealed class StockItemsController(ProcurementContext context) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "procurement.read")]
    public async Task<ActionResult<PagedResult<StockItem>>> Get(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] bool belowReorderLevel = false, CancellationToken cancellationToken = default)
    {
        ValidatePage(page, pageSize);
        var query = context.StockItems.AsNoTracking();
        if (belowReorderLevel) query = query.Where(item => item.QuantityOnHand <= item.ReorderLevel);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(item => item.Sku)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return Ok(new PagedResult<StockItem>(items, page, pageSize, total));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "procurement.read")]
    public async Task<ActionResult<StockItem>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await context.StockItems.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Authorize(Policy = "procurement.write")]
    public async Task<ActionResult<StockItem>> Create([FromBody] CreateStockItemRequest request, CancellationToken cancellationToken)
    {
        var item = new StockItem(Guid.NewGuid(), request.CatalogItemId, request.Sku, request.Description,
            request.UnitSymbol, request.StorageLocation, request.ReorderLevel);
        await context.StockItems.AddAsync(item, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
    }

    [HttpPost("{id:guid}/movements")]
    [Authorize(Policy = "procurement.write")]
    public async Task<ActionResult<StockMovement>> RecordMovement(
        Guid id, [FromBody] RecordStockMovementRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.MovementType))
            throw new ArgumentOutOfRangeException(nameof(request.MovementType));
        var item = await context.StockItems.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null) return NotFound();

        var signedQuantity = request.MovementType switch
        {
            StockMovementType.Receipt or StockMovementType.Return or StockMovementType.TransferIn or StockMovementType.Adjustment => request.Quantity,
            StockMovementType.Consumption or StockMovementType.TransferOut => -request.Quantity,
            _ => throw new ArgumentOutOfRangeException(nameof(request.MovementType))
        };
        item.ApplyMovement(signedQuantity);
        var movement = new StockMovement(Guid.NewGuid(), item.Id, request.MovementType, request.Quantity,
            request.SourceDocument, request.ProjectId, request.Notes);
        await context.StockMovements.AddAsync(movement, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return Created($"/api/v1/stock-items/{id}/movements/{movement.Id}", movement);
    }

    [HttpGet("{id:guid}/movements")]
    [Authorize(Policy = "procurement.read")]
    public async Task<ActionResult<PagedResult<StockMovement>>> GetMovements(
        Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        ValidatePage(page, pageSize);
        if (!await context.StockItems.AnyAsync(x => x.Id == id, cancellationToken)) return NotFound();
        var query = context.StockMovements.AsNoTracking().Where(movement => movement.StockItemId == id);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(movement => movement.CreatedDate)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return Ok(new PagedResult<StockMovement>(items, page, pageSize, total));
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page), "Page must be greater than zero.");
        if (pageSize is < 1 or > 200) throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be between 1 and 200.");
    }
}

public sealed record CreateStockItemRequest(Guid? CatalogItemId, string Sku, string Description,
    string UnitSymbol, string StorageLocation, decimal ReorderLevel);
public sealed record RecordStockMovementRequest(StockMovementType MovementType, decimal Quantity,
    string SourceDocument, Guid? ProjectId = null, string? Notes = null);