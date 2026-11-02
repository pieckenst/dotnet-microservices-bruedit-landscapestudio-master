using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project.Application.DTOs;
using Project.Application.Features.Clients;

namespace Project.API.Controllers;

[ApiController]
[Route("api/v1/clients")]
[Authorize]
public sealed class ClientsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "projects.read")]
    public async Task<ActionResult<PagedResponse<ClientDto>>> Get(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] bool includeArchived = false, CancellationToken cancellationToken = default) =>
        Ok(await mediator.Send(new GetClientsQuery(page, pageSize, includeArchived), cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "projects.read")]
    public async Task<ActionResult<ClientDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetClientByIdQuery(id), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = "projects.write")]
    public async Task<ActionResult<ClientDto>> Create(CreateClientCommand command, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "projects.write")]
    public async Task<ActionResult<ClientDto>> Update(Guid id, UpdateClientCommand command, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command with { Id = id }, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{id:guid}/archive")]
    [Authorize(Policy = "projects.write")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        var changed = await mediator.Send(new ArchiveClientCommand(id), cancellationToken);
        return changed ? NoContent() : NotFound();
    }
}
