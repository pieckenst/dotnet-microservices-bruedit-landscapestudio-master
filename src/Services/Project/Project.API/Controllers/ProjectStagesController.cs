using EventBus.Messages.Events;
using IntegrationProjectStatus = EventBus.Messages.Enums.ProjectStatus;
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
    IRepositoryBase<LandscapeProject> projectRepository,
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
                var tasks = await taskRepository.GetAsync(predicate: task => task.StageId == stage.Id, orderBy: null, includeString: null, disableTracking: true);
                var completedCount = tasks.LongCount(task => task.Status == ProjectTaskStatus.Completed);
                var project = await projectRepository.GetByIdAsync(stage.ProjectId);
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
                    ProjectStatus = MapProjectStatus(project?.Status ?? Project.Domain.Enums.ProjectStatus.Draft)
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

    private static IntegrationProjectStatus MapProjectStatus(Project.Domain.Enums.ProjectStatus status) => status switch
    {
        Project.Domain.Enums.ProjectStatus.Draft => IntegrationProjectStatus.Draft,
        Project.Domain.Enums.ProjectStatus.Planned => IntegrationProjectStatus.Planned,
        Project.Domain.Enums.ProjectStatus.InProgress => IntegrationProjectStatus.InProgress,
        Project.Domain.Enums.ProjectStatus.OnHold => IntegrationProjectStatus.OnHold,
        Project.Domain.Enums.ProjectStatus.Completed => IntegrationProjectStatus.Completed,
        Project.Domain.Enums.ProjectStatus.Cancelled => IntegrationProjectStatus.Cancelled,
        _ => IntegrationProjectStatus.Draft
    };
}
