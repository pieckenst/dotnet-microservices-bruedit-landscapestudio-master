using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project.Application.DTOs;
using Project.Application.Features.Projects;

namespace Project.API.Controllers;

[ApiController]
[Route("api/v1/projects")]
[Authorize]
public sealed class ProjectsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "projects.read")]
    public async Task<ActionResult<PagedResponse<LandscapeProjectDto>>> Get(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] Project.Domain.Enums.ProjectStatus? status = null,
        CancellationToken cancellationToken = default) =>
        Ok(await mediator.Send(new GetProjectsQuery(page, pageSize, status), cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "projects.read")]
    public async Task<ActionResult<LandscapeProjectDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetProjectByIdQuery(id), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = "projects.write")]
    [ProducesResponseType(typeof(LandscapeProjectDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<LandscapeProjectDto>> Create(CreateProjectCommand command, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "projects.write")]
    public async Task<ActionResult<LandscapeProjectDto>> Update(Guid id, UpdateProjectCommand command, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command with { Id = id }, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = "projects.write")]
    public async Task<ActionResult<LandscapeProjectDto>> ChangeStatus(Guid id, ChangeProjectStatusCommand command, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command with { Id = id }, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
