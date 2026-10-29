using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project.Application.DTOs;
using Project.Application.Features.Stages;
using Project.Domain.Enums;

namespace Project.API.Controllers;

[ApiController]
[Route("api/v1/project-stages")]
[Authorize]
public sealed class ProjectStagesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "projects.read")]
    public async Task<ActionResult<PagedResponse<ProjectStageDto>>> Get(
        [FromQuery] Guid projectId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await mediator.Send(new GetProjectStagesQuery(projectId, page, pageSize), cancellationToken));

    [HttpPost]
    [Authorize(Policy = "projects.write")]
    public async Task<ActionResult<ProjectStageDto>> Create(CreateProjectStageCommand command, CancellationToken cancellationToken)
    {
        var stage = await mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(Get), new { projectId = stage.ProjectId }, stage);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "projects.write")]
    public async Task<ActionResult<ProjectStageDto>> Update(Guid id, UpdateProjectStageCommand command, CancellationToken cancellationToken)
    {
        var stage = await mediator.Send(command with { Id = id }, cancellationToken);
        return stage is null ? NotFound() : Ok(stage);
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = "projects.write")]
    public async Task<ActionResult<ProjectStageDto>> ChangeStatus(Guid id, [FromQuery] StageStatus status, CancellationToken cancellationToken)
    {
        var stage = await mediator.Send(new ChangeProjectStageStatusCommand(id, status), cancellationToken);
        return stage is null ? NotFound() : Ok(stage);
    }
}
