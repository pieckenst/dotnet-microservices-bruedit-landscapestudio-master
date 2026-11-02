using EventBus.Messages.Events;
using Organization.Domain.Entities;
using Shared.Common.Persistence;
using Shared.ServiceKit.Outbox;
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
    IRepositoryBase<Employee> employees,
    IIntegrationOutbox outbox) : ControllerBase
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
        Employee? employee = null;
        if (assignment.EmployeeId.HasValue)
        {
            employee = await employees.GetByIdAsync(assignment.EmployeeId.Value);
            if (employee is null)
                throw new InvalidOperationException("The employee for the new assignment could not be loaded.");
        }

        // The same durable event contract represents either an individual employee
        // assignment or a whole-brigade assignment. The analytics consumer stores only
        // stable identifiers and deliberately drops employee names.
        outbox.Enqueue(new EmployeeAssignedEvent
        {
            EmployeeId = assignment.EmployeeId,
            EmployeeFullName = employee?.FullName ?? string.Empty,
            ProjectId = assignment.ProjectId,
            BrigadeId = assignment.BrigadeId,
            AssignmentRole = assignment.Title,
            IsResponsible = false,
            AssignedDate = DateTime.SpecifyKind(
                assignment.StartDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc)
        });

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
