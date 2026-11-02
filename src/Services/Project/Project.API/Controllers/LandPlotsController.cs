using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project.Application.DTOs;
using Project.Application.Features.LandPlots;

namespace Project.API.Controllers;

[ApiController]
[Route("api/v1/land-plots")]
[Authorize]
public sealed class LandPlotsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "projects.read")]
    [ProducesResponseType(typeof(PagedResponse<LandPlotDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<LandPlotDto>>> Get(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] bool includeArchived = false, CancellationToken cancellationToken = default) =>
        Ok(await mediator.Send(new GetLandPlotsQuery(page, pageSize, includeArchived), cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "projects.read")]
    [ProducesResponseType(typeof(LandPlotDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LandPlotDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetLandPlotByIdQuery(id), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = "projects.write")]
    public async Task<ActionResult<LandPlotDto>> Create(CreateLandPlotCommand command, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "projects.write")]
    public async Task<ActionResult<LandPlotDto>> Update(Guid id, UpdateLandPlotCommand command, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command with { Id = id }, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{id:guid}/archive")]
    [Authorize(Policy = "projects.write")]
    public async Task<IActionResult> Archive(Guid id, [FromQuery] bool archived = true, CancellationToken cancellationToken = default)
    {
        var changed = await mediator.Send(new ArchiveLandPlotCommand(id, archived), cancellationToken);
        return changed ? NoContent() : NotFound();
    }
}
