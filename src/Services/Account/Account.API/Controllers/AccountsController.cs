using Account.API.Models;
using Account.Domain.Entities;
using Account.Infrastructure.Persistence;
using EventBus.Messages.Events;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Account.API.Controllers;

/// <summary>
/// Owns user profile/contact data, addresses and preferences. Authentication credentials,
/// roles and token issuance are exclusively owned by the Identity service.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/accounts")]
public sealed class AccountsController(
    AccountContext context,
    IPublishEndpoint publishEndpoint,
    ILogger<AccountsController> logger) : ControllerBase
{
    [HttpGet("me")]
    [ProducesResponseType(typeof(AccountProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AccountProfileDto>> GetMyProfile(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var profile = await EnsureProfileAsync(userId.Value, cancellationToken);
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

        var profile = await EnsureProfileAsync(userId.Value, cancellationToken);
        try
        {
            profile.Update(request.DisplayName, request.FirstName, request.LastName,
                request.Email, request.PhoneNumber, request.CompanyName);
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid profile", Detail = exception.Message });
        }

        await PublishProfileUpdatedAsync(profile, "profile");
        return Ok(AccountProfileDto.From(profile));
    }

    [HttpGet("me/preferences")]
    [ProducesResponseType(typeof(AccountPreferenceDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AccountPreferenceDto>> GetMyPreferences(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        await EnsureProfileAsync(userId.Value, cancellationToken);
        var preferences = await context.AccountPreferences.FindAsync([userId.Value], cancellationToken);
        if (preferences is null)
        {
            preferences = new AccountPreference(userId.Value);
            await context.AccountPreferences.AddAsync(preferences, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }
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
        await EnsureProfileAsync(userId.Value, cancellationToken);
        var preferences = await context.AccountPreferences.FindAsync([userId.Value], cancellationToken);
        if (preferences is null)
        {
            preferences = new AccountPreference(userId.Value);
            await context.AccountPreferences.AddAsync(preferences, cancellationToken);
        }

        try
        {
            preferences.Update(request.Theme, request.Language, request.TimeZone,
                request.EmailNotificationsEnabled, request.SmsNotificationsEnabled);
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid preferences", Detail = exception.Message });
        }
        return Ok(AccountPreferenceDto.From(preferences));
    }

    [HttpGet("me/addresses")]
    [ProducesResponseType(typeof(IReadOnlyList<AccountAddressDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AccountAddressDto>>> GetMyAddresses(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        await EnsureProfileAsync(userId.Value, cancellationToken);
        var addresses = await context.AccountAddresses.AsNoTracking()
            .Where(x => x.UserId == userId.Value)
            .OrderByDescending(x => x.IsDefault).ThenBy(x => x.Label)
            .Select(x => AccountAddressDto.From(x))
            .ToListAsync(cancellationToken);
        return Ok(addresses);
    }

    [HttpPost("me/addresses")]
    [ProducesResponseType(typeof(AccountAddressDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AccountAddressDto>> CreateAddress(
        AccountAddressRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        await EnsureProfileAsync(userId.Value, cancellationToken);
        if (request.IsDefault)
            await ClearDefaultAddressAsync(userId.Value, null, cancellationToken);

        AccountAddress address;
        try
        {
            address = new AccountAddress(Guid.NewGuid(), userId.Value, request.Label, request.Line1,
                request.City, request.Country);
            address.Update(request.Label, request.Line1, request.Line2, request.City,
                request.Region, request.PostalCode, request.Country, request.IsDefault);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid address", Detail = exception.Message });
        }

        await context.AccountAddresses.AddAsync(address, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return Created($"/api/v1/accounts/me/addresses/{address.Id}", AccountAddressDto.From(address));
    }

    [HttpPut("me/addresses/{addressId:guid}")]
    [ProducesResponseType(typeof(AccountAddressDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountAddressDto>> UpdateAddress(
        Guid addressId, AccountAddressRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var address = await context.AccountAddresses.FirstOrDefaultAsync(
            x => x.Id == addressId && x.UserId == userId.Value, cancellationToken);
        if (address is null) return NotFound();

        if (request.IsDefault)
            await ClearDefaultAddressAsync(userId.Value, addressId, cancellationToken);
        try
        {
            address.Update(request.Label, request.Line1, request.Line2, request.City,
                request.Region, request.PostalCode, request.Country, request.IsDefault);
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid address", Detail = exception.Message });
        }
        return Ok(AccountAddressDto.From(address));
    }

    [HttpDelete("me/addresses/{addressId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAddress(Guid addressId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var address = await context.AccountAddresses.FirstOrDefaultAsync(
            x => x.Id == addressId && x.UserId == userId.Value, cancellationToken);
        if (address is null) return NotFound();
        context.AccountAddresses.Remove(address);
        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private Guid? GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id) && id != Guid.Empty ? id : null;
    }

    private async Task<AccountProfile> EnsureProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await context.AccountProfiles.FindAsync([userId], cancellationToken);
        if (profile is not null) return profile;

        var displayName = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(displayName)) displayName = $"User {userId.ToString("N")[..8]}";
        profile = new AccountProfile(userId, displayName);
        await context.AccountProfiles.AddAsync(profile, cancellationToken);
        if (await context.AccountPreferences.FindAsync([userId], cancellationToken) is null)
            await context.AccountPreferences.AddAsync(new AccountPreference(userId), cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Created a default Account profile for identity {UserId}", userId);
        return profile;
    }

    private async Task ClearDefaultAddressAsync(Guid userId, Guid? exceptId, CancellationToken cancellationToken)
    {
        var existing = await context.AccountAddresses
            .Where(x => x.UserId == userId && x.IsDefault && (!exceptId.HasValue || x.Id != exceptId.Value))
            .ToListAsync(cancellationToken);
        foreach (var address in existing)
            address.Update(address.Label, address.Line1, address.Line2, address.City,
                address.Region, address.PostalCode, address.Country, false);
    }

    private async Task PublishProfileUpdatedAsync(AccountProfile profile, string updatedFields)
    {
        try
        {
            await publishEndpoint.Publish(new ProfileUpdatedEvent
            {
                UserId = profile.Id,
                FullName = profile.DisplayName,
                Email = profile.Email ?? string.Empty,
                UpdatedFields = updatedFields,
                UpdatedAt = DateTime.UtcNow
            }, HttpContext.RequestAborted);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Account profile was saved but its profile-updated event could not be published for {UserId}", profile.Id);
        }
    }
}
