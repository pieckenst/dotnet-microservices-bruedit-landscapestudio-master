using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Organization.Application.DTOs;
using Organization.Application.Features.Positions;

namespace Organization.API.Controllers;

[ApiController]
[Route("api/v1/positions")]
[Authorize]
public sealed class PositionsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "organization.read")]
    public async Task<ActionResult<PagedResponse<PositionDto>>> Get(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default) =>
        Ok(await mediator.Send(new GetPositionsQuery(page, pageSize, includeInactive), cancellationToken));

    [HttpPost]
    [Authorize(Policy = "organization.write")]
    public async Task<ActionResult<PositionDto>> Create(CreatePositionCommand command, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(Get), new { page = 1, pageSize = 50 }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "organization.write")]
    public async Task<ActionResult<PositionDto>> Update(Guid id, UpdatePositionCommand command, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command with { Id = id }, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
