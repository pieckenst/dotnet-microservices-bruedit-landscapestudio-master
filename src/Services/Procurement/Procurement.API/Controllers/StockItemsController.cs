using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Procurement.Application.DTOs;
using Procurement.Application.Features.Stock;
using Procurement.Domain.Enums;
using Shared.Common.Persistence;

namespace Procurement.API.Controllers;

[ApiController]
[Route("api/v1/stock-items")]
[Authorize]
public sealed class StockItemsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "procurement.read")]
    public async Task<ActionResult<PagedResult<StockItemDto>>> Get(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] bool belowReorderLevel = false, CancellationToken cancellationToken = default) =>
        Ok(await mediator.Send(new GetStockItemsQuery(page, pageSize, belowReorderLevel), cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "procurement.read")]
    public async Task<ActionResult<StockItemDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await mediator.Send(new GetStockItemByIdQuery(id), cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Authorize(Policy = "procurement.write")]
    public async Task<ActionResult<StockItemDto>> Create(
        [FromBody] CreateStockItemRequest request, CancellationToken cancellationToken)
    {
        var item = await mediator.Send(new CreateStockItemCommand(request.CatalogItemId, request.Sku,
            request.Description, request.UnitSymbol, request.StorageLocation, request.ReorderLevel), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
    }

    [HttpPost("{id:guid}/movements")]
    [Authorize(Policy = "procurement.write")]
    public async Task<ActionResult<StockMovementDto>> RecordMovement(
        Guid id, [FromBody] RecordStockMovementRequest request, CancellationToken cancellationToken)
    {
        var movement = await mediator.Send(new RecordStockMovementCommand(id, request.MovementType,
            request.Quantity, request.SourceDocument, request.ProjectId, request.Notes), cancellationToken);
        return Created($"/api/v1/stock-items/{id}/movements/{movement.Id}", movement);
    }

    [HttpGet("{id:guid}/movements")]
    [Authorize(Policy = "procurement.read")]
    public async Task<ActionResult<PagedResult<StockMovementDto>>> GetMovements(
        Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await mediator.Send(new GetStockMovementsQuery(id, page, pageSize), cancellationToken));
}

public sealed record CreateStockItemRequest(Guid? CatalogItemId, string Sku, string Description,
    string UnitSymbol, string StorageLocation, decimal ReorderLevel);
public sealed record RecordStockMovementRequest(StockMovementType MovementType, decimal Quantity,
    string SourceDocument, Guid? ProjectId = null, string? Notes = null);