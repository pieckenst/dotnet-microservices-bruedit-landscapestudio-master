using System.Text.Json;
using LandscapeStudio.Web.Models;
using LandscapeStudio.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace LandscapeStudio.Web.Controllers;

public sealed class AuditController(LandscapeApiClient api, ILogger<AuditController> logger) : Controller
{
    public async Task<IActionResult> Index(
        DateTime? fromUtc, DateTime? toUtc, string? sourceService, string? action,
        CancellationToken cancellationToken)
    {
        var model = new AuditListViewModel
        {
            FromUtc = fromUtc,
            ToUtc = toUtc,
            SourceService = sourceService,
            Action = action
        };

        var query = BuildQuery(fromUtc, toUtc, sourceService, action);
        try
        {
            var response = await api.GetAsync<JsonElement>("/AuditEvents" + query, cancellationToken);
            model.Items = ReadAuditRows(response);
            model.TotalCount = ReadLong(response, "totalCount", "TotalCount");
        }
        catch (Exception exception) when (exception is ApiException or HttpRequestException or TaskCanceledException or JsonException)
        {
            model.Error = exception.Message;
            logger.LogWarning(exception, "Audit event query failed.");
        }
        return View(model);
    }

    public async Task<IActionResult> Export(
        DateTime? fromUtc, DateTime? toUtc, string? sourceService, string? action,
        CancellationToken cancellationToken)
    {
        try
        {
            var file = await api.GetFileAsync("/AuditEvents/export" + BuildQuery(fromUtc, toUtc, sourceService, action), cancellationToken);
            return File(file.Content, file.ContentType, file.FileName);
        }
        catch (ApiException exception)
        {
            TempData["Error"] = exception.Message;
            return RedirectToAction(nameof(Index), new { fromUtc, toUtc, sourceService, action });
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(exception, "Audit export failed due to service connectivity.");
            TempData["Error"] = "The audit service could not be reached.";
            return RedirectToAction(nameof(Index), new { fromUtc, toUtc, sourceService, action });
        }
    }

    private static string BuildQuery(DateTime? fromUtc, DateTime? toUtc, string? sourceService, string? action)
    {
        var parameters = new List<string> { "page=1", "pageSize=100" };
        if (fromUtc.HasValue) parameters.Add("fromUtc=" + Uri.EscapeDataString(AsUtc(fromUtc.Value).ToString("O")));
        if (toUtc.HasValue) parameters.Add("toUtc=" + Uri.EscapeDataString(AsUtc(toUtc.Value).ToString("O")));
        if (!string.IsNullOrWhiteSpace(sourceService)) parameters.Add("sourceService=" + Uri.EscapeDataString(sourceService.Trim()));
        if (!string.IsNullOrWhiteSpace(action)) parameters.Add("action=" + Uri.EscapeDataString(action.Trim()));
        return "?" + string.Join("&", parameters);
    }

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static long ReadLong(JsonElement root, params string[] names)
    {
        foreach (var name in names)
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var value) &&
                value.TryGetInt64(out var number)) return number;
        return 0;
    }

    private static IReadOnlyList<Dictionary<string, string>> ReadAuditRows(JsonElement root)
    {
        var items = root;
        if (root.ValueKind == JsonValueKind.Object &&
            (root.TryGetProperty("items", out var lower) || root.TryGetProperty("Items", out lower)))
            items = lower;

        if (items.ValueKind != JsonValueKind.Array) return Array.Empty<Dictionary<string, string>>();
        var result = new List<Dictionary<string, string>>();
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in item.EnumerateObject())
            {
                if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array or JsonValueKind.Null)
                    continue;
                row[property.Name] = property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString() ?? string.Empty
                    : property.Value.ToString();
            }
            result.Add(row);
        }
        return result;
    }
}
