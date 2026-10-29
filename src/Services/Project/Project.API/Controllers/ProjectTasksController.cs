using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project.Application.DTOs;
using Project.Application.Features.Tasks;
using Project.Domain.Enums;

namespace Project.API.Controllers;

[ApiController]
[Route("api/v1/project-tasks")]
[Authorize]
public sealed class ProjectTasksController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "projects.read")]
    public async Task<ActionResult<PagedResponse<ProjectTaskDto>>> Get(
        [FromQuery] Guid projectId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] Guid? stageId = null, [FromQuery] ProjectTaskStatus? status = null,
        CancellationToken cancellationToken = default) =>
        Ok(await mediator.Send(new GetProjectTasksQuery(projectId, page, pageSize, stageId, status), cancellationToken));

    [HttpPost]
    [Authorize(Policy = "projects.write")]
    public async Task<ActionResult<ProjectTaskDto>> Create(CreateProjectTaskCommand command, CancellationToken cancellationToken)
    {
        var task = await mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(Get), new { projectId = task.ProjectId }, task);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "projects.write")]
    public async Task<ActionResult<ProjectTaskDto>> Update(Guid id, UpdateProjectTaskCommand command, CancellationToken cancellationToken)
    {
        var task = await mediator.Send(command with { Id = id }, cancellationToken);
        return task is null ? NotFound() : Ok(task);
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = "projects.write")]
    public async Task<ActionResult<ProjectTaskDto>> ChangeStatus(Guid id, [FromQuery] ProjectTaskStatus status, CancellationToken cancellationToken)
    {
        var task = await mediator.Send(new ChangeProjectTaskStatusCommand(id, status), cancellationToken);
        return task is null ? NotFound() : Ok(task);
    }
}
