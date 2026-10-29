using EventBus.Messages.Events;
using MassTransit;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project.Application.DTOs;
using Project.Application.Features.Stages;
using Project.Domain.Entities;
using Project.Domain.Enums;
using Shared.Common.Persistence;

namespace Project.API.Controllers;

[ApiController]
[Route("api/v1/project-stages")]
[Authorize]
public sealed class ProjectStagesController(
    IMediator mediator,
    IRepositoryBase<ProjectStage> stageRepository,
    IRepositoryBase<ProjectTask> taskRepository,
    IPublishEndpoint publishEndpoint,
    ILogger<ProjectStagesController> logger) : ControllerBase
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
    public async Task<ActionResult<ProjectStageDto>> ChangeStatus(
        Guid id, [FromQuery] StageStatus status, CancellationToken cancellationToken)
    {
        var previous = await stageRepository.GetByIdAsync(id);
        var stage = await mediator.Send(new ChangeProjectStageStatusCommand(id, status), cancellationToken);
        if (stage is null) return NotFound();

        if (stage.Status == StageStatus.Completed && previous?.Status != StageStatus.Completed)
        {
            try
            {
                // Project tasks already provide a real source for completion counts.
                var tasks = await taskRepository.GetAsync(task => task.StageId == stage.Id);
                var completedCount = tasks.LongCount(task => task.Status == ProjectTaskStatus.Completed);
                await publishEndpoint.Publish(new ProjectStageCompletedEvent
                {
                    ProjectId = stage.ProjectId,
                    StageId = stage.Id,
                    StageName = stage.Name,
                    CompletedTaskCount = checked((int)completedCount),
                    TotalTaskCount = tasks.Count,
                    CompletedDate = DateTime.SpecifyKind(
                        (stage.ActualEndDate ?? DateOnly.FromDateTime(DateTime.UtcNow)).ToDateTime(TimeOnly.MinValue),
                        DateTimeKind.Utc),
                    ProjectStatus = ProjectStatus.Unknown
                }, cancellationToken);
            }
            catch (Exception exception)
            {
                // The stage status was already saved; event transport failure must not misreport the write.
                logger.LogError(exception, "Stage {StageId} was completed but its audit event could not be published.", stage.Id);
            }
        }
        return Ok(stage);
    }
}
