using Account.API.Models;
using Account.Application.Features.Profiles;
using EventBus.Messages.Events;
using MassTransit;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Account.API.Controllers;

/// <summary>
/// Owns user profile, contact, address and preference data. Credentials and role assignments
/// are exclusively owned by Identity. All use cases are dispatched through the Application layer.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/accounts")]
public sealed class AccountsController(IMediator mediator, IPublishEndpoint publishEndpoint) : ControllerBase
{
    [HttpGet("me")]
    [ProducesResponseType(typeof(AccountProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AccountProfileDto>> GetMyProfile(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var profile = await mediator.Send(new GetMyProfileQuery(userId.Value, User.Identity?.Name), cancellationToken);
        return Ok(AccountProfileDto.From(profile));
    }

    [HttpPut("me")]
    [ProducesResponseType(typeof(AccountProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AccountProfileDto>> UpdateMyProfile(
        UpdateAccountProfileRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        try
        {
            var profile = await mediator.Send(new UpdateMyProfileCommand(
                userId.Value, request.DisplayName, request.FirstName, request.LastName,
                request.Email, request.PhoneNumber, request.CompanyName), cancellationToken);

            // Persist the profile before publishing the integration event. A transport failure
            // is not presented as a successful update and is logged by the host's exception pipeline.
            await publishEndpoint.Publish(new ProfileUpdatedEvent
            {
                UserId = profile.Id,
                UpdatedFields = "profile",
                UpdatedAt = DateTime.UtcNow
            }, cancellationToken);

            return Ok(AccountProfileDto.From(profile));
        }
        catch (Exception exception) when (exception is ArgumentException or FluentValidation.ValidationException)
        {
            return InvalidInput(exception, "Invalid profile");
        }
    }

    [HttpGet("me/preferences")]
    [ProducesResponseType(typeof(AccountPreferenceDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AccountPreferenceDto>> GetMyPreferences(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var preferences = await mediator.Send(
            new GetMyPreferencesQuery(userId.Value, User.Identity?.Name), cancellationToken);
        return Ok(AccountPreferenceDto.From(preferences));
    }

    [HttpPut("me/preferences")]
    [ProducesResponseType(typeof(AccountPreferenceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AccountPreferenceDto>> UpdateMyPreferences(
        UpdateAccountPreferenceRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        try
        {
            var preferences = await mediator.Send(new UpdateMyPreferencesCommand(
                userId.Value, request.Theme, request.Language, request.TimeZone,
                request.EmailNotificationsEnabled, request.SmsNotificationsEnabled), cancellationToken);
            return Ok(AccountPreferenceDto.From(preferences));
        }
        catch (Exception exception) when (exception is ArgumentException or FluentValidation.ValidationException)
        {
            return InvalidInput(exception, "Invalid preferences");
        }
    }

    [HttpGet("me/addresses")]
    [ProducesResponseType(typeof(IReadOnlyList<AccountAddressDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AccountAddressDto>>> GetMyAddresses(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var addresses = await mediator.Send(
            new GetMyAddressesQuery(userId.Value, User.Identity?.Name), cancellationToken);
        return Ok(addresses.Select(AccountAddressDto.From).ToArray());
    }

    [HttpPost("me/addresses")]
    [ProducesResponseType(typeof(AccountAddressDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AccountAddressDto>> CreateAddress(
        AccountAddressRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        try
        {
            var address = await mediator.Send(new CreateMyAddressCommand(
                userId.Value, User.Identity?.Name, request.Label, request.Line1, request.Line2,
                request.City, request.Region, request.PostalCode, request.Country, request.IsDefault),
                cancellationToken);
            return Created($"/api/v1/accounts/me/addresses/{address.Id}", AccountAddressDto.From(address));
        }
        catch (Exception exception) when (exception is ArgumentException or FluentValidation.ValidationException)
        {
            return InvalidInput(exception, "Invalid address");
        }
    }

    [HttpPut("me/addresses/{addressId:guid}")]
    [ProducesResponseType(typeof(AccountAddressDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountAddressDto>> UpdateAddress(
        Guid addressId, AccountAddressRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        try
        {
            var address = await mediator.Send(new UpdateMyAddressCommand(
                userId.Value, addressId, request.Label, request.Line1, request.Line2,
                request.City, request.Region, request.PostalCode, request.Country, request.IsDefault),
                cancellationToken);
            return address is null ? NotFound() : Ok(AccountAddressDto.From(address));
        }
        catch (Exception exception) when (exception is ArgumentException or FluentValidation.ValidationException)
        {
            return InvalidInput(exception, "Invalid address");
        }
    }

    [HttpDelete("me/addresses/{addressId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAddress(Guid addressId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var deleted = await mediator.Send(new DeleteMyAddressCommand(userId.Value, addressId), cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    private Guid? GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id) && id != Guid.Empty ? id : null;
    }

    private ActionResult InvalidInput(Exception exception, string title)
    {
        if (exception is FluentValidation.ValidationException validation)
        {
            var errors = validation.Errors
                .GroupBy(error => error.PropertyName)
                .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).Distinct().ToArray());
            return ValidationProblem(errors, title: title);
        }

        return BadRequest(new ProblemDetails
        {
            Title = title,
            Detail = exception.Message,
            Status = StatusCodes.Status400BadRequest
        });
    }
}
