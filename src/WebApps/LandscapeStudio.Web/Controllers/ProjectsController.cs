using System.Text.Json;
using LandscapeStudio.Web.Models;
using LandscapeStudio.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace LandscapeStudio.Web.Controllers;

public sealed class ProjectsController(LandscapeApiClient api, ILogger<ProjectsController> logger) : Controller
{
    public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        try
        {
            var result = await api.GetAsync<ApiPage<LandscapeProjectDto>>(
                $"/Projects?page={page}&pageSize=50", cancellationToken);
            return View(new ProjectsIndexViewModel(result.Items, result.Page, result.TotalPages, result.TotalCount));
        }
        catch (Exception exception) when (exception is ApiException or HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(exception, "Project list could not be loaded.");
            return View(new ProjectsIndexViewModel(Array.Empty<LandscapeProjectDto>(), page, 0, 0, exception.Message));
        }
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var model = await LoadCreateModelAsync(new CreateProjectViewModel(), cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateProjectViewModel model, CancellationToken cancellationToken)
    {
        if (model.ClientId == Guid.Empty) ModelState.AddModelError(nameof(model.ClientId), "Choose a client.");
        if (model.LandPlotId == Guid.Empty) ModelState.AddModelError(nameof(model.LandPlotId), "Choose a land plot.");
        if (model.PlannedStartDate is not null && model.PlannedEndDate is not null &&
            model.PlannedEndDate < model.PlannedStartDate)
            ModelState.AddModelError(nameof(model.PlannedEndDate), "The planned end date cannot precede the start date.");

        model = await LoadCreateModelAsync(model, cancellationToken);
        if (!ModelState.IsValid) return View(model);

        try
        {
            await api.PostAsync<CreateProjectRequest, LandscapeProjectDto>("/Projects",
                new CreateProjectRequest(model.ProjectNumber.Trim(), model.Name.Trim(),
                    model.ClientId, model.LandPlotId, model.Description.Trim(),
                    model.PlannedStartDate, model.PlannedEndDate),
                cancellationToken: cancellationToken);
            TempData["Success"] = $"Project {model.ProjectNumber.Trim()} was created.";
            return RedirectToAction(nameof(Index));
        }
        catch (ApiException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(model);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            ModelState.AddModelError(string.Empty, "The project service could not be reached.");
            logger.LogWarning(exception, "Project creation failed due to service connectivity.");
            return View(model);
        }
    }

    private async Task<CreateProjectViewModel> LoadCreateModelAsync(
        CreateProjectViewModel model, CancellationToken cancellationToken)
    {
        try
        {
            var clients = await api.GetAsync<JsonElement>("/Clients?page=1&pageSize=200", cancellationToken);
            var plots = await api.GetAsync<JsonElement>("/LandPlots?page=1&pageSize=200", cancellationToken);
            model.Clients = ReadItems<ClientDto>(clients).Where(x => !x.IsArchived).ToArray();
            model.LandPlots = ReadItems<LandPlotDto>(plots).Where(x => !x.IsArchived).ToArray();
        }
        catch (Exception exception) when (exception is ApiException or HttpRequestException or TaskCanceledException or JsonException)
        {
            ModelState.AddModelError(string.Empty, "Clients or land plots could not be loaded. Verify the Project API and your read permissions.");
            logger.LogWarning(exception, "Project create form dependencies were unavailable.");
        }
        return model;
    }

    private static IReadOnlyList<T> ReadItems<T>(JsonElement response)
    {
        var items = response;
        if (response.ValueKind == JsonValueKind.Object &&
            (response.TryGetProperty("items", out var lower) || response.TryGetProperty("Items", out lower)))
            items = lower;
        return items.ValueKind == JsonValueKind.Array
            ? items.Deserialize<List<T>>(new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? []
            : [];
    }
}

public sealed record ProjectsIndexViewModel(
    IReadOnlyList<LandscapeProjectDto> Items, int Page, int TotalPages, int TotalCount, string? Error = null);
