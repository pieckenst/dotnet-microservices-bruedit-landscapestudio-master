using System.Text.Json;
using LandscapeStudio.Web.Models;
using LandscapeStudio.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace LandscapeStudio.Web.Controllers;

public sealed class OrganizationController(
    LandscapeApiClient api,
    ILogger<OrganizationController> logger) : Controller
{
    public async Task<IActionResult> Assignments(CancellationToken cancellationToken)
    {
        try
        {
            var assignmentsTask = api.GetAsync<JsonElement>("/WorkAssignments?page=1&pageSize=100", cancellationToken);
            var projectsTask = api.GetAsync<JsonElement>("/Projects?page=1&pageSize=200", cancellationToken);
            var employeesTask = api.GetAsync<JsonElement>("/Employees?page=1&pageSize=200", cancellationToken);
            var brigadesTask = api.GetAsync<JsonElement>("/Brigades?page=1&pageSize=200", cancellationToken);
            await Task.WhenAll(assignmentsTask, projectsTask, employeesTask, brigadesTask);

            var projectNames = ReadItems(projectsTask.Result)
                .Where(item => ReadGuid(item, "id") != Guid.Empty)
                .ToDictionary(
                    item => ReadGuid(item, "id"),
                    item => $"{ReadString(item, "projectNumber")} — {ReadString(item, "name")}");
            var employeeNames = ReadItems(employeesTask.Result)
                .Where(item => ReadGuid(item, "id") != Guid.Empty)
                .ToDictionary(
                    item => ReadGuid(item, "id"),
                    item => $"{ReadString(item, "employeeNumber")} — {ReadString(item, "fullName")}");
            var brigadeNames = ReadItems(brigadesTask.Result)
                .Where(item => ReadGuid(item, "id") != Guid.Empty)
                .ToDictionary(
                    item => ReadGuid(item, "id"),
                    item => ReadString(item, "name"));

            var rows = ReadItems(assignmentsTask.Result)
                .Select(item =>
                {
                    var projectId = ReadGuid(item, "projectId");
                    var employeeId = ReadNullableGuid(item, "employeeId");
                    var brigadeId = ReadNullableGuid(item, "brigadeId");
                    var assignedTo = employeeId.HasValue && employeeNames.TryGetValue(employeeId.Value, out var employee)
                        ? employee
                        : brigadeId.HasValue && brigadeNames.TryGetValue(brigadeId.Value, out var brigade)
                            ? brigade ?? "Unknown brigade"
                            : "Unassigned";
                    return new WorkAssignmentRow(
                        ReadGuid(item, "id"),
                        projectNames.TryGetValue(projectId, out var project) ? project : projectId.ToString("D"),
                        assignedTo,
                        ReadString(item, "title") ?? string.Empty,
                        ReadDate(item, "startDate") ?? DateOnly.MinValue,
                        ReadNullableDate(item, "endDate"),
                        ReadEnum(item, "status"),
                        ReadString(item, "notes") ?? string.Empty);
                })
                .Where(item => item.Id != Guid.Empty)
                .OrderByDescending(item => item.StartDate)
                .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return View(new WorkAssignmentListViewModel { Items = rows });
        }
        catch (Exception exception) when (exception is ApiException or HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(exception, "Could not load Organization work assignments.");
            return View(new WorkAssignmentListViewModel
            {
                Error = "Work assignments could not be loaded. Check Organization, Project and your organization.read/projects.read permissions."
            });
        }
    }

    [HttpGet]
    public async Task<IActionResult> CreateAssignment(CancellationToken cancellationToken)
    {
        var model = new WorkAssignmentFormViewModel();
        await LoadLookupsAsync(model, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAssignment(
        WorkAssignmentFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (model.ProjectId == Guid.Empty)
            ModelState.AddModelError(nameof(model.ProjectId), "Choose a project.");
        if (model.EmployeeId.HasValue == model.BrigadeId.HasValue)
            ModelState.AddModelError(string.Empty, "Choose exactly one assignment target: an employee or a brigade.");
        if (model.EmployeeId == Guid.Empty || model.BrigadeId == Guid.Empty || model.ProjectTaskId == Guid.Empty)
            ModelState.AddModelError(string.Empty, "Optional identifiers must be valid GUIDs or left blank.");
        if (string.IsNullOrWhiteSpace(model.Title) || model.Title.Trim().Length > 160)
            ModelState.AddModelError(nameof(model.Title), "Enter a title of 1 to 160 characters.");
        if (model.Notes?.Length > 2000)
            ModelState.AddModelError(nameof(model.Notes), "Notes cannot exceed 2,000 characters.");
        if (model.EndDate.HasValue && model.EndDate.Value < model.StartDate)
            ModelState.AddModelError(nameof(model.EndDate), "End date cannot precede the start date.");

        await LoadLookupsAsync(model, cancellationToken);
        if (model.EmployeeId.HasValue && !model.Employees.Any(x => x.Id == model.EmployeeId.Value))
            ModelState.AddModelError(nameof(model.EmployeeId), "Choose an active employee.");
        if (model.BrigadeId.HasValue && !model.Brigades.Any(x => x.Id == model.BrigadeId.Value))
            ModelState.AddModelError(nameof(model.BrigadeId), "Choose an active brigade.");
        if (model.ProjectId != Guid.Empty && !model.Projects.Any(x => x.Id == model.ProjectId))
            ModelState.AddModelError(nameof(model.ProjectId), "Choose an active project.");

        if (!ModelState.IsValid)
            return View(model);

        try
        {
            await api.PostAsync<object, JsonElement>("/WorkAssignments", new
            {
                employeeId = model.EmployeeId,
                brigadeId = model.BrigadeId,
                projectId = model.ProjectId,
                projectTaskId = model.ProjectTaskId,
                title = model.Title.Trim(),
                startDate = model.StartDate,
                endDate = model.EndDate,
                notes = model.Notes?.Trim() ?? string.Empty
            }, cancellationToken: cancellationToken);

            TempData["Success"] = "Work assignment created.";
            return RedirectToAction(nameof(Assignments));
        }
        catch (ApiException exception)
        {
            logger.LogWarning(exception, "Organization rejected a work assignment.");
            ModelState.AddModelError(string.Empty, exception.Message);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogError(exception, "Could not create a work assignment.");
            ModelState.AddModelError(string.Empty,
                "The Organization service could not confirm the assignment. Refresh the list before retrying.");
        }

        await LoadLookupsAsync(model, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeAssignmentStatus(Guid id, string status, CancellationToken cancellationToken)
    {
        var allowed = new[] { "Planned", "Active", "Completed", "Cancelled" };
        if (id == Guid.Empty || !allowed.Contains(status, StringComparer.OrdinalIgnoreCase))
            return BadRequest();

        try
        {
            await api.SendAsync<JsonElement>(HttpMethod.Patch,
                $"/WorkAssignments/{id:D}/status?status={Uri.EscapeDataString(status)}",
                cancellationToken: cancellationToken);
            TempData["Success"] = $"Assignment status changed to {status}.";
        }
        catch (Exception exception) when (exception is ApiException or HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(exception, "Could not change work assignment {AssignmentId} status to {Status}.", id, status);
            TempData["Error"] = $"Assignment status change failed: {exception.Message}";
        }

        return RedirectToAction(nameof(Assignments));
    }

    private async Task LoadLookupsAsync(WorkAssignmentFormViewModel model, CancellationToken cancellationToken)
    {
        model.LookupError = null;
        try
        {
            var projectsTask = api.GetAsync<JsonElement>("/Projects?page=1&pageSize=200", cancellationToken);
            var employeesTask = api.GetAsync<JsonElement>("/Employees?page=1&pageSize=200", cancellationToken);
            var brigadesTask = api.GetAsync<JsonElement>("/Brigades?page=1&pageSize=200", cancellationToken);
            await Task.WhenAll(projectsTask, employeesTask, brigadesTask);

            model.Projects = ReadItems(projectsTask.Result)
                .Where(item => !ReadBoolean(item, "isArchived"))
                .Select(item => new AssignmentProjectOption(
                    ReadGuid(item, "id"),
                    ReadString(item, "projectNumber") ?? string.Empty,
                    ReadString(item, "name") ?? string.Empty))
                .Where(item => item.Id != Guid.Empty)
                .OrderBy(item => item.ProjectNumber)
                .ToArray();

            model.Employees = ReadItems(employeesTask.Result)
                .Where(item => ReadEnum(item, "status").Equals("Active", StringComparison.OrdinalIgnoreCase) ||
                    ReadEnum(item, "status").Equals("0", StringComparison.Ordinal))
                .Select(item => new AssignmentEmployeeOption(
                    ReadGuid(item, "id"),
                    ReadString(item, "employeeNumber") ?? string.Empty,
                    ReadString(item, "fullName") ?? string.Empty))
                .Where(item => item.Id != Guid.Empty)
                .OrderBy(item => item.EmployeeNumber)
                .ToArray();

            model.Brigades = ReadItems(brigadesTask.Result)
                .Where(item => ReadBoolean(item, "isActive"))
                .Select(item => new AssignmentBrigadeOption(
                    ReadGuid(item, "id"),
                    ReadString(item, "name") ?? string.Empty))
                .Where(item => item.Id != Guid.Empty)
                .OrderBy(item => item.Name)
                .ToArray();
        }
        catch (Exception exception) when (exception is ApiException or HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(exception, "Organization assignment lookups could not be loaded.");
            model.LookupError = "Projects, employees or brigades could not be loaded. Check the owning services and their read permissions.";
            model.Projects = Array.Empty<AssignmentProjectOption>();
            model.Employees = Array.Empty<AssignmentEmployeeOption>();
            model.Brigades = Array.Empty<AssignmentBrigadeOption>();
        }
    }

    private static IReadOnlyList<JsonElement> ReadItems(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array) return root.EnumerateArray().ToArray();
        if (root.ValueKind == JsonValueKind.Object &&
            (TryGetProperty(root, "items", out var items) || TryGetProperty(root, "data", out items)) &&
            items.ValueKind == JsonValueKind.Array)
            return items.EnumerateArray().ToArray();
        return Array.Empty<JsonElement>();
    }

    private static string ReadEnum(JsonElement source, string name)
    {
        if (!TryGetProperty(source, name, out var value)) return string.Empty;
        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString();
    }

    private static DateOnly? ReadDate(JsonElement source, string name) =>
        DateOnly.TryParse(ReadString(source, name), System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var date) ? date : null;

    private static DateOnly? ReadNullableDate(JsonElement source, string name) => ReadDate(source, name);

    private static Guid ReadGuid(JsonElement source, string name) =>
        Guid.TryParse(ReadString(source, name), out var id) ? id : Guid.Empty;

    private static Guid? ReadNullableGuid(JsonElement source, string name) =>
        Guid.TryParse(ReadString(source, name), out var id) && id != Guid.Empty ? id : null;

    private static string? ReadString(JsonElement source, string name) =>
        TryGetProperty(source, name, out var value) && value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined)
            ? value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString()
            : null;

    private static bool ReadBoolean(JsonElement source, string name) =>
        TryGetProperty(source, name, out var value) && value.ValueKind == JsonValueKind.True;

    private static bool TryGetProperty(JsonElement source, string name, out JsonElement value)
    {
        if (source.ValueKind == JsonValueKind.Object)
            foreach (var property in source.EnumerateObject())
                if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
        value = default;
        return false;
    }
}
