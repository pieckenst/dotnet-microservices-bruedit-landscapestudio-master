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
            var estimate = await api.PostAsync<CreateEstimateRequest, EstimateDto>("/Estimates",
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
        var model = await LoadDetailsAsync(id, cancellationToken);
        return model.Estimate is null ? (model.Error is null ? NotFound() : View(model)) : View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddLineItem(Guid id, AddEstimateLineItemViewModel model, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return BadRequest();
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
                $"/Estimates/{id:D}/line-items", model.ToRequest(), cancellationToken: cancellationToken);
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
            await api.PostAsync<object, EstimateDto>($"/Estimates/{id:D}/{action}", new { }, cancellationToken: cancellationToken);
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
            model.Estimate = await api.GetAsync<EstimateDto>($"/Estimates/{id:D}", cancellationToken);
            try
            {
                var projects = await api.GetAsync<ApiPage<LandscapeProjectDto>>("/Projects?page=1&pageSize=200", cancellationToken);
                model.Project = projects.Items.FirstOrDefault(p => p.Id == model.Estimate.ProjectId);
            }
            catch (Exception exception) when (exception is ApiException or HttpRequestException or TaskCanceledException)
            {
                logger.LogWarning(exception, "Project label for estimate {EstimateId} could not be loaded.", id);
            }
            model.NewLineItem.EstimateId = id;
        }
        catch (Exception exception) when (exception is ApiException or HttpRequestException or TaskCanceledException)
        {
            model.Error = exception.Message;
            logger.LogWarning(exception, "Estimate details failed for {EstimateId}.", id);
        }
        return model;
    }
}
