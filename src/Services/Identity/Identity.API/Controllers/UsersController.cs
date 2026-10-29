using Identity.Application.DTOs;
using Identity.Application.Features.Users;
using EventBus.Messages.Events;
using MassTransit;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Controllers;

[ApiController]
[Authorize(Roles = "Administrator")]
[Route("api/v1/users")]
public sealed class UsersController(IMediator mediator, IPublishEndpoint publishEndpoint) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<IdentityUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResponse<IdentityUserDto>>> Get(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default) =>
        Ok(await mediator.Send(new GetUsersQuery(page, pageSize), cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(IdentityUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IdentityUserDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var user = await mediator.Send(new GetUserByIdQuery(id), cancellationToken);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPost]
    [ProducesResponseType(typeof(IdentityUserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<IdentityUserDto>> Create(CreateUserCommand command, CancellationToken cancellationToken)
    {
        var user = await mediator.Send(command, cancellationToken);
        await publishEndpoint.Publish(new UserRegisteredEvent
        {
            UserId = user.Id,
            Username = user.UserName,
            RoleName = user.Role,
            RegisteredAt = user.CreatedDate
        }, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
    }
}
