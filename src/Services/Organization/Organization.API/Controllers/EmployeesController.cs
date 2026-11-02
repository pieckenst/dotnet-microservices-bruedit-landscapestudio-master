using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Organization.Application.DTOs;
using Organization.Application.Features.Employees;
using Organization.Domain.Enums;

namespace Organization.API.Controllers;

[ApiController]
[Route("api/v1/employees")]
[Authorize]
public sealed class EmployeesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "organization.read")]
    public async Task<ActionResult<PagedResponse<EmployeeDto>>> Get(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] EmployeeStatus? status = null,
        CancellationToken cancellationToken = default) =>
        Ok(await mediator.Send(new GetEmployeesQuery(page, pageSize, status), cancellationToken));

    [HttpPost]
    [Authorize(Policy = "organization.write")]
    public async Task<ActionResult<EmployeeDto>> Create(CreateEmployeeCommand command, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(Get), new { page = 1, pageSize = 50 }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "organization.write")]
    public async Task<ActionResult<EmployeeDto>> Update(Guid id, UpdateEmployeeCommand command, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command with { Id = id }, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = "organization.write")]
    public async Task<ActionResult<EmployeeDto>> ChangeStatus(Guid id, [FromQuery] EmployeeStatus status, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ChangeEmployeeStatusCommand(id, status), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
