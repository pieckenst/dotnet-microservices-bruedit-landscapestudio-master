using EventBus.Messages.Events;
using MassTransit;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Organization.Application.DTOs;
using Organization.Application.Features.Assignments;
using Organization.Domain.Enums;

namespace Organization.API.Controllers;

[ApiController]
[Route("api/v1/work-assignments")]
[Authorize]
public sealed class WorkAssignmentsController(
    IMediator mediator,
    IPublishEndpoint publishEndpoint,
    ILogger<WorkAssignmentsController> logger) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "organization.read")]
    public async Task<ActionResult<PagedResponse<WorkAssignmentDto>>> Get(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] Guid? projectId = null,
        [FromQuery] AssignmentStatus? status = null, CancellationToken cancellationToken = default) =>
        Ok(await mediator.Send(new GetWorkAssignmentsQuery(page, pageSize, projectId, status), cancellationToken));

    [HttpPost]
    [Authorize(Policy = "organization.write")]
    public async Task<ActionResult<WorkAssignmentDto>> Create(CreateWorkAssignmentCommand command, CancellationToken cancellationToken)
    {
        var assignment = await mediator.Send(command, cancellationToken);
        if (assignment.EmployeeId.HasValue)
        {
            try
            {
                await publishEndpoint.Publish(new EmployeeAssignedEvent
                {
                    EmployeeId = assignment.EmployeeId.Value,
                    EmployeeFullName = string.Empty,
                    ProjectId = assignment.ProjectId,
                    BrigadeId = assignment.BrigadeId ?? Guid.Empty,
                    AssignmentRole = assignment.Title,
                    IsResponsible = false,
                    AssignedDate = DateTime.SpecifyKind(
                        assignment.StartDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc)
                }, cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Work assignment {AssignmentId} was saved but its analytics event could not be published.", assignment.Id);
            }
        }

        return CreatedAtAction(nameof(Get), new { projectId = assignment.ProjectId }, assignment);
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = "organization.write")]
    public async Task<ActionResult<WorkAssignmentDto>> ChangeStatus(Guid id, [FromQuery] AssignmentStatus status, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ChangeWorkAssignmentStatusCommand(id, status), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
