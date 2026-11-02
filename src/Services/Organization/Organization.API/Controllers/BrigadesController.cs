using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Organization.Application.DTOs;
using Organization.Application.Features.Brigades;
using Organization.Domain.Enums;

namespace Organization.API.Controllers;

[ApiController]
[Route("api/v1/brigades")]
[Authorize]
public sealed class BrigadesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "organization.read")]
    public async Task<ActionResult<PagedResponse<BrigadeDto>>> Get(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default) =>
        Ok(await mediator.Send(new GetBrigadesQuery(page, pageSize, includeInactive), cancellationToken));

    [HttpPost]
    [Authorize(Policy = "organization.write")]
    public async Task<ActionResult<BrigadeDto>> Create(CreateBrigadeCommand command, CancellationToken cancellationToken)
    {
        var brigade = await mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(Get), new { page = 1, pageSize = 50 }, brigade);
    }

    [HttpPost("{id:guid}/members")]
    [Authorize(Policy = "organization.write")]
    public async Task<ActionResult<BrigadeDto>> AddMember(Guid id, AddBrigadeMemberCommand command, CancellationToken cancellationToken)
    {
        var brigade = await mediator.Send(command with { BrigadeId = id }, cancellationToken);
        return brigade is null ? NotFound() : Ok(brigade);
    }

    [HttpDelete("{id:guid}/members/{employeeId:guid}")]
    [Authorize(Policy = "organization.write")]
    public async Task<ActionResult<BrigadeDto>> RemoveMember(Guid id, Guid employeeId, [FromQuery] DateOnly leaveDate,
        CancellationToken cancellationToken)
    {
        var brigade = await mediator.Send(new RemoveBrigadeMemberCommand(id, employeeId, leaveDate), cancellationToken);
        return brigade is null ? NotFound() : Ok(brigade);
    }
}
