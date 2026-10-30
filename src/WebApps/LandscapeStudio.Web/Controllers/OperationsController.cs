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
            ["clients"] = ("Clients", "/Clients?page=1&pageSize=100"),
            ["land-plots"] = ("Land plots", "/LandPlots?page=1&pageSize=100"),
            ["plants"] = ("Plant catalog", "/Catalog/Plants?page=1&pageSize=100"),
            ["materials"] = ("Material catalog", "/Catalog/Materials?page=1&pageSize=100"),
            ["work-types"] = ("Work types", "/Catalog/WorkTypes?page=1&pageSize=100"),
            ["suppliers"] = ("Suppliers", "/Suppliers?page=1&pageSize=100"),
            ["stock-items"] = ("Inventory", "/StockItems?page=1&pageSize=100"),
            ["purchase-orders"] = ("Purchase orders", "/PurchaseOrders?page=1&pageSize=100"),
            ["deliveries"] = ("Supplier deliveries", "/Deliveries?page=1&pageSize=100")
        };

    private static readonly IReadOnlyDictionary<string, string> CreateEndpoints =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["clients"] = "/Clients",
            ["land-plots"] = "/LandPlots",
            ["plants"] = "/Catalog/Plants",
            ["materials"] = "/Catalog/Materials",
            ["work-types"] = "/Catalog/WorkTypes",
            ["suppliers"] = "/Suppliers",
            ["stock-items"] = "/StockItems"
        };

    // Edit/archive routes are deliberately limited to Project-owned reference data
    // until the other owning services expose the complete update lifecycle.
    private static readonly IReadOnlyDictionary<string, string> UpdateEndpoints =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["clients"] = "/Clients",
            ["land-plots"] = "/LandPlots"
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

    [HttpGet]
    public async Task<IActionResult> Create(string resource, CancellationToken cancellationToken)
    {
        if (!CreateEndpoints.ContainsKey(resource))
            return NotFound();

        var model = new OperationFormViewModel
        {
            Resource = resource,
            Title = Resources[resource].Title,
            IsActive = true,
            CurrencyCode = "BYN",
            PaymentTermDays = 30
        };
        await LoadLookupsAsync(model, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(OperationFormViewModel model, CancellationToken cancellationToken)
    {
        var resource = model.Resource;
        if (string.IsNullOrWhiteSpace(resource) || !CreateEndpoints.TryGetValue(resource, out var endpoint))
            return NotFound();

        model.Resource = resource;
        model.Title = Resources[resource].Title;
        ValidateForResource(model);
        if (!ModelState.IsValid)
        {
            await LoadLookupsAsync(model, cancellationToken);
            return View(model);
        }

        try
        {
            await api.PostAsync<object, JsonElement>(
                endpoint, BuildPayload(model), cancellationToken: cancellationToken);
            TempData["Success"] = $"{model.Title.TrimEnd('.')}: “{GetRecordName(model)}” created successfully.";
            return RedirectToAction(nameof(Index), new { resource = model.Resource });
        }
        catch (ApiException exception)
        {
            logger.LogWarning(exception, "The {Resource} API rejected a create request.", model.Resource);
            ModelState.AddModelError(string.Empty, exception.Message);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogError(exception, "Could not create a record in {Resource}.", model.Resource);
            ModelState.AddModelError(string.Empty, "The service could not be reached or returned an invalid response. The record was not confirmed as created.");
        }

        await LoadLookupsAsync(model, cancellationToken);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string resource, Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty || !UpdateEndpoints.TryGetValue(resource, out var endpoint))
            return NotFound();

        try
        {
            var item = await api.GetAsync<JsonElement>($"{endpoint}/{id:D}", cancellationToken);
            var model = new OperationFormViewModel
            {
                Resource = resource,
                Title = Resources[resource].Title,
                IsActive = true,
                Name = ReadString(item, resource.Equals("clients", StringComparison.OrdinalIgnoreCase) ? "displayName" : "name") ?? string.Empty,
                Email = ReadString(item, "email"),
                Phone = ReadString(item, "phone"),
                Address = ReadString(item, resource.Equals("clients", StringComparison.OrdinalIgnoreCase) ? "billingAddress" : "address") ?? string.Empty,
                AreaSquareMetres = ReadDecimal(item, "areaSquareMetres"),
                SoilType = ReadString(item, "soilType"),
                Notes = ReadString(item, "notes")
            };
            ViewData["EditId"] = id;
            return View("Create", model);
        }
        catch (ApiException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return NotFound();
        }
        catch (Exception exception) when (exception is ApiException or HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(exception, "Could not load {Resource} record {RecordId} for editing.", resource, id);
            TempData["Error"] = "The record could not be loaded. Check the owning service and your permissions.";
            return RedirectToAction(nameof(Index), new { resource });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string resource, Guid id, OperationFormViewModel model, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty || !UpdateEndpoints.TryGetValue(resource, out var endpoint))
            return NotFound();

        model.Resource = resource;
        model.Title = Resources[resource].Title;
        ValidateForResource(model);
        ViewData["EditId"] = id;
        if (!ModelState.IsValid)
            return View("Create", model);

        try
        {
            await api.SendAsync<JsonElement>(HttpMethod.Put, $"{endpoint}/{id:D}",
                BuildUpdatePayload(model, id), cancellationToken: cancellationToken);
            TempData["Success"] = $"{model.Title.TrimEnd('.')}: “{GetRecordName(model)}” updated successfully.";
            return RedirectToAction(nameof(Index), new { resource });
        }
        catch (ApiException exception)
        {
            logger.LogWarning(exception, "The {Resource} API rejected update for record {RecordId}.", resource, id);
            ModelState.AddModelError(string.Empty, exception.Message);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogError(exception, "Could not confirm update for {Resource} record {RecordId}.", resource, id);
            ModelState.AddModelError(string.Empty, "The service could not confirm the update. Refresh the list before retrying.");
        }

        return View("Create", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(string resource, Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty || !UpdateEndpoints.ContainsKey(resource))
            return NotFound();

        var endpoint = UpdateEndpoints[resource];
        var path = resource.Equals("clients", StringComparison.OrdinalIgnoreCase)
            ? $"{endpoint}/{id:D}/archive"
            : $"{endpoint}/{id:D}/archive?archived=true";

        try
        {
            await api.SendAsync<object>(HttpMethod.Post, path, new { }, cancellationToken: cancellationToken);
            TempData["Success"] = $"{Resources[resource].Title.TrimEnd('.')}: record archived.";
        }
        catch (Exception exception) when (exception is ApiException or HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(exception, "Could not archive {Resource} record {RecordId}.", resource, id);
            TempData["Error"] = $"Archive failed: {exception.Message}";
        }

        return RedirectToAction(nameof(Index), new { resource });
    }

    private static object BuildUpdatePayload(OperationFormViewModel model, Guid id) =>
        model.Resource.Equals("clients", StringComparison.OrdinalIgnoreCase)
            ? new
            {
                id,
                displayName = model.Name.Trim(),
                email = Clean(model.Email),
                phone = Clean(model.Phone),
                billingAddress = Clean(model.Address)
            }
            : new
            {
                id,
                name = model.Name.Trim(),
                address = model.Address.Trim(),
                areaSquareMetres = model.AreaSquareMetres,
                soilType = Clean(model.SoilType),
                notes = Clean(model.Notes)
            };

    private async Task LoadLookupsAsync(OperationFormViewModel model, CancellationToken cancellationToken)
    {
        if (!IsCatalogItem(model.Resource)) return;
        try
        {
            var page = await api.GetAsync<ApiPage<MeasurementUnitOption>>(
                "/Catalog/MeasurementUnits?page=1&pageSize=200", cancellationToken);
            model.MeasurementUnits = page.Items.Where(unit => unit.IsActive)
                .OrderBy(unit => unit.Name).ToArray();
            if (model.MeasurementUnits.Count == 0)
                model.LookupError = "No active measurement units exist. Create a measurement unit in Catalog before adding this record.";
        }
        catch (Exception exception) when (exception is ApiException or HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(exception, "Measurement units could not be loaded for {Resource}.", model.Resource);
            model.LookupError = "Measurement units could not be loaded. Check Catalog service availability and your catalog.read permission.";
            model.MeasurementUnits = Array.Empty<MeasurementUnitOption>();
        }
    }

    private void ValidateForResource(OperationFormViewModel model)
    {
        static bool Missing(string? value) => string.IsNullOrWhiteSpace(value);
        switch (model.Resource.ToLowerInvariant())
        {
            case "clients":
                if (Missing(model.Name)) ModelState.AddModelError(nameof(model.Name), "Client display name is required.");
                else if (model.Name.Trim().Length > 160) ModelState.AddModelError(nameof(model.Name), "Client display name cannot exceed 160 characters.");
                break;
            case "land-plots":
                if (Missing(model.Name)) ModelState.AddModelError(nameof(model.Name), "Land plot name is required.");
                if (Missing(model.Address)) ModelState.AddModelError(nameof(model.Address), "Land plot address is required.");
                if (model.AreaSquareMetres is < 0.01m or > 1_000_000_000m)
                    ModelState.AddModelError(nameof(model.AreaSquareMetres), "Area must be between 0.01 and 1,000,000,000 square metres.");
                break;
            case "plants":
                if (Missing(model.Name)) ModelState.AddModelError(nameof(model.Name), "Plant name is required.");
                if (model.MeasurementUnitId == Guid.Empty) ModelState.AddModelError(nameof(model.MeasurementUnitId), "Choose an active measurement unit.");
                break;
            case "materials":
                if (Missing(model.Name)) ModelState.AddModelError(nameof(model.Name), "Material name is required.");
                if (Missing(model.Sku)) ModelState.AddModelError(nameof(model.Sku), "Material SKU is required.");
                if (model.MeasurementUnitId == Guid.Empty) ModelState.AddModelError(nameof(model.MeasurementUnitId), "Choose an active measurement unit.");
                break;
            case "work-types":
                if (Missing(model.Name)) ModelState.AddModelError(nameof(model.Name), "Work type name is required.");
                if (model.MeasurementUnitId == Guid.Empty) ModelState.AddModelError(nameof(model.MeasurementUnitId), "Choose an active measurement unit.");
                break;
            case "suppliers":
                if (Missing(model.Code)) ModelState.AddModelError(nameof(model.Code), "Supplier code is required.");
                if (Missing(model.Name)) ModelState.AddModelError(nameof(model.Name), "Supplier name is required.");
                if (Missing(model.ContactName)) ModelState.AddModelError(nameof(model.ContactName), "Contact name is required.");
                if (Missing(model.Address)) ModelState.AddModelError(nameof(model.Address), "Supplier address is required.");
                if (Missing(model.CurrencyCode) || model.CurrencyCode.Trim().Length != 3 ||
                    !model.CurrencyCode.All(char.IsLetter))
                    ModelState.AddModelError(nameof(model.CurrencyCode), "Enter a three-letter currency code.");
                if (model.PaymentTermDays is < 0 or > 365)
                    ModelState.AddModelError(nameof(model.PaymentTermDays), "Payment terms must be between 0 and 365 days.");
                break;
            case "stock-items":
                if (Missing(model.Sku)) ModelState.AddModelError(nameof(model.Sku), "Stock SKU is required.");
                if (Missing(model.Description)) ModelState.AddModelError(nameof(model.Description), "Stock description is required.");
                if (Missing(model.UnitSymbol)) ModelState.AddModelError(nameof(model.UnitSymbol), "Unit symbol is required.");
                if (Missing(model.StorageLocation)) ModelState.AddModelError(nameof(model.StorageLocation), "Storage location is required.");
                break;
        }
    }

    private static object BuildPayload(OperationFormViewModel model) =>
        model.Resource.ToLowerInvariant() switch
        {
            "clients" => new
            {
                displayName = model.Name.Trim(),
                email = Clean(model.Email),
                phone = Clean(model.Phone),
                billingAddress = Clean(model.Address)
            },
            "land-plots" => new
            {
                name = model.Name.Trim(),
                address = model.Address.Trim(),
                areaSquareMetres = model.AreaSquareMetres,
                soilType = Clean(model.SoilType),
                notes = Clean(model.Notes)
            },
            "plants" => new
            {
                name = model.Name.Trim(), latinName = Clean(model.LatinName),
                description = model.Description ?? string.Empty, measurementUnitId = model.MeasurementUnitId,
                unitPrice = model.UnitPrice, isActive = model.IsActive
            },
            "materials" => new
            {
                name = model.Name.Trim(), sku = model.Sku.Trim().ToUpperInvariant(),
                description = model.Description ?? string.Empty, measurementUnitId = model.MeasurementUnitId,
                unitPrice = model.UnitPrice, isActive = model.IsActive
            },
            "work-types" => new
            {
                name = model.Name.Trim(), description = model.Description ?? string.Empty,
                measurementUnitId = model.MeasurementUnitId, basePrice = model.BasePrice, isActive = model.IsActive
            },
            "suppliers" => new
            {
                code = model.Code.Trim().ToUpperInvariant(), name = model.Name.Trim(),
                contactName = model.ContactName.Trim(), email = Clean(model.Email), phone = Clean(model.Phone),
                taxIdentifier = Clean(model.TaxIdentifier), address = model.Address.Trim(),
                paymentTermDays = model.PaymentTermDays, currencyCode = model.CurrencyCode.Trim().ToUpperInvariant()
            },
            "stock-items" => new
            {
                catalogItemId = model.CatalogItemId, sku = model.Sku.Trim().ToUpperInvariant(),
                description = model.Description.Trim(), unitSymbol = model.UnitSymbol.Trim(),
                storageLocation = model.StorageLocation.Trim(), reorderLevel = model.ReorderLevel
            },
            _ => throw new InvalidOperationException("The selected operation is not supported.")
        };

    private static string? ReadString(JsonElement source, string property) =>
        source.ValueKind == JsonValueKind.Object &&
        source.TryGetProperty(property, out var value) &&
        value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined)
            ? value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString()
            : null;

    private static decimal ReadDecimal(JsonElement source, string property) =>
        decimal.TryParse(ReadString(source, property),
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture,
            out var value) ? value : 0m;

    private static bool IsCatalogItem(string? resource) =>
        resource is not null && resource.Equals("plants", StringComparison.OrdinalIgnoreCase) ||
        resource is not null && resource.Equals("materials", StringComparison.OrdinalIgnoreCase) ||
        resource is not null && resource.Equals("work-types", StringComparison.OrdinalIgnoreCase);

    private static string GetRecordName(OperationFormViewModel model) =>
        model.Resource.Equals("suppliers", StringComparison.OrdinalIgnoreCase) ? model.Name :
        model.Resource.Equals("stock-items", StringComparison.OrdinalIgnoreCase) ? model.Sku :
        model.Name;

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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
