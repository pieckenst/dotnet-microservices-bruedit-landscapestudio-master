using System.Globalization;
using System.Text.Json;
using LandscapeStudio.Web.Models;
using LandscapeStudio.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace LandscapeStudio.Web.Controllers;

public sealed class EstimatesController(LandscapeApiClient api, ILogger<EstimatesController> logger) : Controller
{
    public async Task<IActionResult> Index(Guid? projectId, int page = 1, CancellationToken cancellationToken = default)
    {
        var model = new EstimatesIndexViewModel { ProjectId = projectId, Page = Math.Max(1, page) };
        try
        {
            model.Projects = (await api.GetAsync<ApiPage<LandscapeProjectDto>>(
                "/Projects?page=1&pageSize=200", cancellationToken)).Items;
            if (projectId.HasValue && projectId.Value != Guid.Empty)
            {
                var response = await api.GetAsync<ApiPage<EstimateDto>>(
                    $"/Estimates?projectId={projectId.Value:D}&page={model.Page}&pageSize=50", cancellationToken);
                model.Estimates = response.Items;
                model.Page = response.Page;
                model.TotalPages = response.TotalPages;
                model.TotalCount = response.TotalCount;
            }
        }
        catch (Exception exception) when (exception is ApiException or HttpRequestException or TaskCanceledException)
        {
            model.Error = exception.Message;
            logger.LogWarning(exception, "Estimate listing failed.");
        }
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Create(Guid? projectId, CancellationToken cancellationToken)
    {
        var model = new CreateEstimateViewModel { ProjectId = projectId ?? Guid.Empty };
        model = await LoadProjectsAsync(model, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateEstimateViewModel model, CancellationToken cancellationToken)
    {
        if (model.ProjectId == Guid.Empty)
            ModelState.AddModelError(nameof(model.ProjectId), "Choose a project.");
        model = await LoadProjectsAsync(model, cancellationToken);
        if (!ModelState.IsValid) return View(model);

        try
        {
            var estimate = await api.PostAsync<CreateEstimateRequest, EstimateDto>("Estimates",
                new CreateEstimateRequest(model.ProjectId, model.Version.Trim(), model.Notes.Trim()),
                cancellationToken: cancellationToken);
            TempData["Success"] = $"Estimate {estimate.Version} was created.";
            return RedirectToAction(nameof(Details), new { id = estimate.Id });
        }
        catch (ApiException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(model);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(exception, "Estimate creation failed due to service connectivity.");
            ModelState.AddModelError(string.Empty, "The Project service could not be reached.");
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest();
        var model = await LoadDetailsAsync(id, cancellationToken);
        return model.Estimate is null ? (model.Error is null ? NotFound() : View(model)) : View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddLineItem(Guid id, AddEstimateLineItemViewModel model, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest();

        if (model.CatalogItemId == Guid.Empty)
            ModelState.AddModelError(nameof(model.CatalogItemId), "Choose a valid Catalog item or leave the Catalog item blank.");

        if (!ModelState.IsValid)
        {
            var invalid = await LoadDetailsAsync(id, cancellationToken);
            invalid.Error = "Review the line-item values and try again.";
            invalid.NewLineItem = model;
            return View("Details", invalid);
        }

        try
        {
            await api.PostAsync<AddEstimateLineItemRequest, EstimateDto>(
                $"Estimates/{id:D}/line-items", model.ToRequest(), cancellationToken: cancellationToken);
            TempData["Success"] = "Estimate line item added.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (ApiException exception)
        {
            var current = await LoadDetailsAsync(id, cancellationToken);
            current.Error = exception.Message;
            current.NewLineItem = model;
            return View("Details", current);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(exception, "Adding estimate line item failed.");
            var current = await LoadDetailsAsync(id, cancellationToken);
            current.Error = "The Project service could not be reached.";
            current.NewLineItem = model;
            return View("Details", current);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Submit(Guid id, CancellationToken cancellationToken) =>
        RunTransitionAsync(id, "submit", "submitted", cancellationToken);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken) =>
        RunTransitionAsync(id, "approve", "approved", cancellationToken);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Reject(Guid id, CancellationToken cancellationToken) =>
        RunTransitionAsync(id, "reject", "rejected", cancellationToken);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Supersede(Guid id, CancellationToken cancellationToken) =>
        RunTransitionAsync(id, "supersede", "marked as superseded", cancellationToken);

    private async Task<IActionResult> RunTransitionAsync(Guid id, string action, string pastTense, CancellationToken cancellationToken)
    {
        try
        {
            await api.PostAsync<object, EstimateDto>($"Estimates/{id:D}/{action}", new { }, cancellationToken: cancellationToken);
            TempData["Success"] = $"Estimate {pastTense}.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (ApiException exception)
        {
            TempData["Error"] = exception.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(exception, "Estimate transition {Transition} failed.", action);
            TempData["Error"] = "The Project service could not be reached.";
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    private async Task<CreateEstimateViewModel> LoadProjectsAsync(CreateEstimateViewModel model, CancellationToken cancellationToken)
    {
        try
        {
            model.Projects = (await api.GetAsync<ApiPage<LandscapeProjectDto>>(
                "/Projects?page=1&pageSize=200", cancellationToken)).Items;
        }
        catch (Exception exception) when (exception is ApiException or HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(exception, "Project references for estimate creation failed.");
            ModelState.AddModelError(string.Empty, "Projects could not be loaded. Check the Project service and your permissions.");
        }
        return model;
    }

    private async Task<EstimateDetailsViewModel> LoadDetailsAsync(Guid id, CancellationToken cancellationToken)
    {
        var model = new EstimateDetailsViewModel();
        try
        {
            model.Estimate = await api.GetAsync<EstimateDto>($"Estimates/{id:D}", cancellationToken);
            try
            {
                var projects = await api.GetAsync<ApiPage<LandscapeProjectDto>>(
                    "/Projects?page=1&pageSize=200", cancellationToken);
                model.Project = projects.Items.FirstOrDefault(project => project.Id == model.Estimate.ProjectId);
            }
            catch (Exception exception) when (exception is ApiException or HttpRequestException or TaskCanceledException)
            {
                logger.LogWarning(exception, "Project label for estimate {EstimateId} could not be loaded.", id);
            }

            model.NewLineItem.EstimateId = id;
            await LoadCatalogItemsAsync(model, cancellationToken);
        }
        catch (Exception exception) when (exception is ApiException or HttpRequestException or TaskCanceledException)
        {
            model.Error = exception.Message;
            logger.LogWarning(exception, "Estimate details failed for {EstimateId}.", id);
        }
        return model;
    }

    private async Task LoadCatalogItemsAsync(EstimateDetailsViewModel model, CancellationToken cancellationToken)
    {
        try
        {
            var unitsResponse = await api.GetAsync<JsonElement>(
                "/Catalog/MeasurementUnits?page=1&pageSize=200", cancellationToken);
            var unitSymbols = ReadRows(unitsResponse)
                .Where(row => ReadBoolean(row, "isActive"))
                .Select(row => new
                {
                    Id = ReadGuid(row, "id"),
                    Symbol = ReadString(row, "symbol")
                })
                .Where(row => row.Id != Guid.Empty && !string.IsNullOrWhiteSpace(row.Symbol))
                .ToDictionary(row => row.Id, row => row.Symbol!, EqualityComparer<Guid>.Default);

            var options = new List<EstimateCatalogItemOption>();
            await AppendCatalogItemsAsync(options, unitSymbols, "/Catalog/WorkTypes", 0, "basePrice", cancellationToken);
            await AppendCatalogItemsAsync(options, unitSymbols, "/Catalog/Materials", 1, "unitPrice", cancellationToken);
            await AppendCatalogItemsAsync(options, unitSymbols, "/Catalog/Plants", 2, "unitPrice", cancellationToken);

            model.CatalogItems = options.OrderBy(option => option.ItemType).ThenBy(option => option.Name).ToArray();
            if (model.CatalogItems.Count == 0)
                model.CatalogError = "No active work types, materials or plants with active measurement units are available in Catalog.";
        }
        catch (Exception exception) when (exception is ApiException or HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            logger.LogWarning(exception, "Catalog items could not be loaded for estimate editing.");
            model.CatalogItems = Array.Empty<EstimateCatalogItemOption>();
            model.CatalogError = exception is ApiException apiException && apiException.IsUnauthorized
                ? "Your account cannot read the Catalog. You can still add a custom estimate line."
                : "Catalog items could not be loaded. You can still add a custom estimate line.";
        }
    }

    private async Task AppendCatalogItemsAsync(
        ICollection<EstimateCatalogItemOption> destination,
        IReadOnlyDictionary<Guid, string> unitSymbols,
        string path,
        int itemType,
        string priceProperty,
        CancellationToken cancellationToken)
    {
        var response = await api.GetAsync<JsonElement>($"{path}?page=1&pageSize=200", cancellationToken);
        foreach (var item in ReadRows(response))
        {
            if (!ReadBoolean(item, "isActive")) continue;
            var id = ReadGuid(item, "id");
            var unitId = ReadGuid(item, "measurementUnitId");
            if (id == Guid.Empty || unitId == Guid.Empty || !unitSymbols.TryGetValue(unitId, out var symbol))
                continue;

            var name = ReadString(item, "name");
            if (string.IsNullOrWhiteSpace(name)) continue;
            var price = ReadDecimal(item, priceProperty);
            var description = ReadString(item, "description") ?? name;

            destination.Add(new EstimateCatalogItemOption(
                id, itemType, name.Trim(), description.Trim(), symbol, price));
        }
    }

    private static IReadOnlyList<JsonElement> ReadRows(JsonElement response)
    {
        var items = response;
        if (response.ValueKind == JsonValueKind.Object &&
            (TryGetProperty(response, "items", out var nested) || TryGetProperty(response, "data", out nested)))
            items = nested;
        if (items.ValueKind != JsonValueKind.Array) return Array.Empty<JsonElement>();
        return items.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.Object)
            .Select(item => item.Clone())
            .ToArray();
    }

    private static bool TryGetProperty(JsonElement source, string name, out JsonElement value)
    {
        if (source.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in source.EnumerateObject())
            {
                if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static string? ReadString(JsonElement source, string name) =>
        TryGetProperty(source, name, out var value)
            ? value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString()
            : null;

    private static Guid ReadGuid(JsonElement source, string name) =>
        Guid.TryParse(ReadString(source, name), out var id) ? id : Guid.Empty;

    private static decimal ReadDecimal(JsonElement source, string name)
    {
        var value = ReadString(source, name);
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var price)
            ? price
            : throw new InvalidOperationException($"Catalog response contains an invalid {name} value.");
    }

    private static bool ReadBoolean(JsonElement source, string name) =>
        TryGetProperty(source, name, out var value) && value.ValueKind == JsonValueKind.True;
}
