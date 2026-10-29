using System.Text.Json;
using LandscapeStudio.Web.Models;
using LandscapeStudio.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace LandscapeStudio.Web.Controllers;

public sealed class AuthController(LandscapeApiClient api) : Controller
{
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (!string.IsNullOrWhiteSpace(HttpContext.Session.GetString("access_token")))
            return RedirectToAction("Index", "Home");

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(model);
        try
        {
            var response = await api.PostAsync<LoginViewModel, AuthResponse>(
                "/Auth/login", new LoginRequest(model.UserName, model.Password), includeToken: false, cancellationToken: cancellationToken);
            HttpContext.Session.SetString("access_token", response.AccessToken);
            HttpContext.Session.SetString("token_expires_utc", response.ExpiresAtUtc.ToUniversalTime().ToString("O"));
            var userName = ReadUserProperty(response.User, "userName") ?? model.UserName;
            var role = ReadUserProperty(response.User, "role") ?? "User";
            HttpContext.Session.SetString("user_name", userName);
            HttpContext.Session.SetString("user_role", role);
            if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                return Redirect(model.ReturnUrl);
            return RedirectToAction("Index", "Home");
        }
        catch (ApiException exception)
        {
            ModelState.AddModelError(string.Empty, exception.IsUnauthorized
                ? "The username or password was not accepted."
                : exception.Message);
            return View(model);
        }
        catch (HttpRequestException)
        {
            ModelState.AddModelError(string.Empty, "The API gateway cannot be reached. Start the services and verify the gateway URL.");
            return View(model);
        }
        catch (TaskCanceledException)
        {
            ModelState.AddModelError(string.Empty, "The API request timed out. Check that the gateway is running.");
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction(nameof(Login));
    }

    private static string? ReadUserProperty(JsonElement user, string property) =>
        user.ValueKind == JsonValueKind.Object &&
        user.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
