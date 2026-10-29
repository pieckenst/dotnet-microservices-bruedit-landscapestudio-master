using System.Globalization;
using System.Text.Json;
using LandscapeStudio.Web.Models;
using LandscapeStudio.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace LandscapeStudio.Web.Controllers;

public sealed class OperationsController(LandscapeApiClient api, ILogger<OperationsController> logger) : Controller
{
    private static readonly IReadOnlyDictionary<string, (string Title, string ApiPath)> Resources =
        new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["plants"] = ("Plant catalog", "/Catalog/Plants?page=1&pageSize=100"),
            ["materials"] = ("Material catalog", "/Catalog/Materials?page=1&pageSize=100"),
            ["work-types"] = ("Work types", "/Catalog/WorkTypes?page=1&pageSize=100"),
            ["suppliers"] = ("Suppliers", "/Suppliers?page=1&pageSize=100"),
            ["stock-items"] = ("Inventory", "/StockItems?page=1&pageSize=100"),
            ["purchase-orders"] = ("Purchase orders", "/PurchaseOrders?page=1&pageSize=100"),
            ["deliveries"] = ("Supplier deliveries", "/Deliveries?page=1&pageSize=100")
        };

    public async Task<IActionResult> Index(string resource = "plants", CancellationToken cancellationToken = default)
    {
        if (!Resources.TryGetValue(resource, out var target))
            return NotFound();

        try
        {
            var response = await api.GetAsync<JsonElement>(target.ApiPath, cancellationToken);
            var rows = ReadRows(response);
            var columns = rows.FirstOrDefault()?.Values.Keys.ToArray() ?? [];
            return View(new OperationListViewModel
            {
                Title = target.Title,
                Resource = resource,
                Columns = columns,
                Items = rows
            });
        }
        catch (Exception exception) when (exception is ApiException or HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(exception, "Could not load the {Resource} workflow.", resource);
            return View(new OperationListViewModel
            {
                Title = target.Title,
                Resource = resource,
                Columns = Array.Empty<string>(),
                Items = Array.Empty<OperationItem>(),
                Error = exception.Message
            });
        }
    }

    private static IReadOnlyList<OperationItem> ReadRows(JsonElement root)
    {
        var items = root;
        if (root.ValueKind == JsonValueKind.Object &&
            (root.TryGetProperty("items", out var lower) || root.TryGetProperty("Items", out lower)))
            items = lower;

        if (items.ValueKind != JsonValueKind.Array) return Array.Empty<OperationItem>();
        var result = new List<OperationItem>();
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in item.EnumerateObject())
            {
                if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array or JsonValueKind.Null or JsonValueKind.Undefined)
                    continue;
                var value = property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString() ?? string.Empty
                    : property.Value.ToString();
                values[SplitName(property.Name)] = value;
            }
            result.Add(new OperationItem { Values = values });
        }
        return result;
    }

    private static string SplitName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return name;
        var chars = new List<char>(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]) && (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1])))
                chars.Add(' ');
            chars.Add(i == 0 ? char.ToUpperInvariant(name[i]) : name[i]);
        }
        return new string(chars.ToArray());
    }
}
