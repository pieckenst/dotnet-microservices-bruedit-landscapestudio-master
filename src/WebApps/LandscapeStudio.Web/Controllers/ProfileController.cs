using LandscapeStudio.Web.Models;
using LandscapeStudio.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace LandscapeStudio.Web.Controllers;

public sealed class ProfileController(LandscapeApiClient api, ILogger<ProfileController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = new ProfilePageViewModel();
        try
        {
            model.Profile = await api.GetAsync<AccountProfileDto>("/Accounts/me", cancellationToken);
            model.Preferences = await api.GetAsync<AccountPreferenceDto>("/Accounts/me/preferences", cancellationToken);
            model.Addresses = await api.GetAsync<IReadOnlyList<AccountAddressDto>>("/Accounts/me/addresses", cancellationToken);
        }
        catch (Exception exception) when (exception is ApiException or HttpRequestException or TaskCanceledException)
        {
            model.Error = exception.Message;
            logger.LogWarning(exception, "Profile data could not be loaded.");
        }
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(UpdateProfileViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return await ReloadWithErrorsAsync(cancellationToken);
        try
        {
            await api.PutAsync<UpdateProfileViewModel, AccountProfileDto>("/Accounts/me", model, cancellationToken);
            TempData["Success"] = "Profile details saved.";
            return RedirectToAction(nameof(Index));
        }
        catch (ApiException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return await ReloadWithErrorsAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            ModelState.AddModelError(string.Empty, "The Account service could not be reached.");
            logger.LogWarning(exception, "Profile update failed due to service connectivity.");
            return await ReloadWithErrorsAsync(cancellationToken);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePreferences(UpdatePreferencesViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return await ReloadWithErrorsAsync(cancellationToken);
        try
        {
            await api.PutAsync<UpdatePreferencesViewModel, AccountPreferenceDto>("/Accounts/me/preferences", model, cancellationToken);
            TempData["Success"] = "Preferences saved.";
            return RedirectToAction(nameof(Index));
        }
        catch (ApiException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return await ReloadWithErrorsAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            ModelState.AddModelError(string.Empty, "The Account service could not be reached.");
            logger.LogWarning(exception, "Preference update failed due to service connectivity.");
            return await ReloadWithErrorsAsync(cancellationToken);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAddress(AddressFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return await ReloadWithErrorsAsync(cancellationToken);
        try
        {
            await api.PostAsync<AccountAddressRequest, AccountAddressDto>("/Accounts/me/addresses",
                model.ToRequest(), cancellationToken: cancellationToken);
            TempData["Success"] = "Address added.";
            return RedirectToAction(nameof(Index));
        }
        catch (ApiException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return await ReloadWithErrorsAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            ModelState.AddModelError(string.Empty, "The Account service could not be reached.");
            logger.LogWarning(exception, "Address creation failed due to service connectivity.");
            return await ReloadWithErrorsAsync(cancellationToken);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAddress(Guid id, AddressFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return await ReloadWithErrorsAsync(cancellationToken);
        try
        {
            await api.PutAsync<AccountAddressRequest, AccountAddressDto>(
                $"/Accounts/me/addresses/{id}", model.ToRequest(), cancellationToken);
            TempData["Success"] = "Address updated.";
            return RedirectToAction(nameof(Index));
        }
        catch (ApiException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return await ReloadWithErrorsAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            ModelState.AddModelError(string.Empty, "The Account service could not be reached.");
            logger.LogWarning(exception, "Address update failed due to service connectivity.");
            return await ReloadWithErrorsAsync(cancellationToken);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAddress(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await api.DeleteAsync($"/Accounts/me/addresses/{id}", cancellationToken);
            TempData["Success"] = "Address deleted.";
        }
        catch (ApiException exception)
        {
            TempData["Error"] = exception.Message;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(exception, "Address deletion failed due to service connectivity.");
            TempData["Error"] = "The Account service could not be reached.";
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> ReloadWithErrorsAsync(CancellationToken cancellationToken)
    {
        var model = new ProfilePageViewModel { Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray() };
        try
        {
            model.Profile = await api.GetAsync<AccountProfileDto>("/Accounts/me", cancellationToken);
            model.Preferences = await api.GetAsync<AccountPreferenceDto>("/Accounts/me/preferences", cancellationToken);
            model.Addresses = await api.GetAsync<IReadOnlyList<AccountAddressDto>>("/Accounts/me/addresses", cancellationToken);
        }
        catch (Exception exception) when (exception is ApiException or HttpRequestException or TaskCanceledException)
        {
            model.Error = exception.Message;
        }
        return View("Index", model);
    }
}

public sealed class ProfilePageViewModel
{
    public AccountProfileDto? Profile { get; set; }
    public AccountPreferenceDto? Preferences { get; set; }
    public IReadOnlyList<AccountAddressDto> Addresses { get; set; } = Array.Empty<AccountAddressDto>();
    public string? Error { get; set; }
    public IReadOnlyList<string> Errors { get; set; } = Array.Empty<string>();
}

public sealed class UpdateProfileViewModel
{
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(120, MinimumLength = 1)]
    public string DisplayName { get; set; } = string.Empty;
    [System.ComponentModel.DataAnnotations.StringLength(80)]
    public string? FirstName { get; set; }
    [System.ComponentModel.DataAnnotations.StringLength(80)]
    public string? LastName { get; set; }
    [System.ComponentModel.DataAnnotations.EmailAddress, System.ComponentModel.DataAnnotations.StringLength(254)]
    public string? Email { get; set; }
    [System.ComponentModel.DataAnnotations.Phone, System.ComponentModel.DataAnnotations.StringLength(40)]
    public string? PhoneNumber { get; set; }
    [System.ComponentModel.DataAnnotations.StringLength(160)]
    public string? CompanyName { get; set; }
}

public sealed class UpdatePreferencesViewModel
{
    [System.ComponentModel.DataAnnotations.Required]
    public string Theme { get; set; } = "system";
    [System.ComponentModel.DataAnnotations.Required]
    public string Language { get; set; } = "en";
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(80)]
    public string TimeZone { get; set; } = "UTC";
    public bool EmailNotificationsEnabled { get; set; } = true;
    public bool SmsNotificationsEnabled { get; set; }
}

public sealed class AddressFormViewModel
{
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(80, MinimumLength = 1)]
    public string Label { get; set; } = string.Empty;
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(160, MinimumLength = 1)]
    public string Line1 { get; set; } = string.Empty;
    [System.ComponentModel.DataAnnotations.StringLength(160)]
    public string? Line2 { get; set; }
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(100, MinimumLength = 1)]
    public string City { get; set; } = string.Empty;
    [System.ComponentModel.DataAnnotations.StringLength(100)]
    public string? Region { get; set; }
    [System.ComponentModel.DataAnnotations.StringLength(24)]
    public string? PostalCode { get; set; }
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(2, MinimumLength = 2)]
    public string Country { get; set; } = "BY";
    public bool IsDefault { get; set; }

    public AccountAddressRequest ToRequest() =>
        new(Label, Line1, Line2, City, Region, PostalCode, Country, IsDefault);
}
