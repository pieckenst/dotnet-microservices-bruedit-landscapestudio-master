using LandscapeStudio.Web.Models;
using LandscapeStudio.Web.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace LandscapeStudio.Web.Controllers;

public sealed class HomeController(LandscapeApiClient api, ILogger<HomeController> logger) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        AnalyticsSummaryViewModel? analytics = null;
        IReadOnlyList<LandscapeProjectDto> projects = Array.Empty<LandscapeProjectDto>();
        string? warning = null;
        try
        {
            analytics = await api.GetAsync<AnalyticsSummaryViewModel>("/Analytics/Summary", cancellationToken);
        }
        catch (Exception exception) when (exception is ApiException or HttpRequestException or TaskCanceledException)
        {
            warning = "Analytics is unavailable right now. Other workspace areas may still be usable.";
            logger.LogWarning(exception, "Dashboard could not load analytics summary.");
        }

        try
        {
            var response = await api.GetAsync<JsonElement>("/Projects?page=1&pageSize=6", cancellationToken);
            projects = ParseItems<LandscapeProjectDto>(response);
        }
        catch (Exception exception) when (exception is ApiException or HttpRequestException or TaskCanceledException or JsonException)
        {
            warning = warning is null
                ? "Projects could not be loaded. Check service connectivity and your permissions."
                : warning + " Projects could not be loaded.";
            logger.LogWarning(exception, "Dashboard could not load projects.");
        }

        return View(new DashboardViewModel(analytics, projects, warning));
    }

    public IActionResult Error() =>
        View(new ErrorViewModel(HttpContext.TraceIdentifier));

    private static IReadOnlyList<T> ParseItems<T>(JsonElement response)
    {
        var value = response;
        if (response.ValueKind == JsonValueKind.Object &&
            (response.TryGetProperty("items", out var lower) || response.TryGetProperty("Items", out lower)))
            value = lower;
        return value.ValueKind == JsonValueKind.Array
            ? value.Deserialize<List<T>>(new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? []
            : [];
    }
}

public sealed record DashboardViewModel(
    AnalyticsSummaryViewModel? Analytics,
    IReadOnlyList<LandscapeProjectDto> RecentProjects,
    string? Warning);
