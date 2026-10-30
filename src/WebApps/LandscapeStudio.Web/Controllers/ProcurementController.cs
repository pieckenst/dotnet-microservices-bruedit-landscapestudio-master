using System.Net;
using System.Text.Json;
using LandscapeStudio.Web.Models;
using LandscapeStudio.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace LandscapeStudio.Web.Controllers;

public sealed class ProcurementController(
    LandscapeApiClient api,
    ILogger<ProcurementController> logger) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = new ProcurementIndexViewModel();
        try
        {
            var page = await api.GetAsync<ApiPage<PurchaseOrderSummary>>(
                "/PurchaseOrders?page=1&pageSize=50", cancellationToken);
            model.Orders = page.Items;
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            logger.LogWarning(exception, "Procurement workspace could not load purchase orders.");
            model.Error = "Purchase orders could not be loaded. Check Procurement service connectivity and your permissions.";
        }

        try
        {
            var page = await api.GetAsync<ApiPage<StockItemSummary>>(
                "/StockItems?page=1&pageSize=50", cancellationToken);
            model.StockItems = page.Items;
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            logger.LogWarning(exception, "Procurement workspace could not load inventory.");
            model.Error = Append(model.Error, "Inventory could not be loaded.");
        }

        try
        {
            var page = await api.GetAsync<ApiPage<SupplierDeliverySummary>>(
                "/Deliveries?page=1&pageSize=50", cancellationToken);
            model.Deliveries = page.Items;
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            logger.LogWarning(exception, "Procurement workspace could not load deliveries.");
            model.Error = Append(model.Error, "Supplier deliveries could not be loaded.");
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> CreateOrder(CancellationToken cancellationToken)
    {
        var model = new CreatePurchaseOrderViewModel();
        await LoadSuppliersAsync(model, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateOrder(CreatePurchaseOrderViewModel model, CancellationToken cancellationToken)
    {
        await LoadSuppliersAsync(model, cancellationToken);
        var lines = model.Lines
            .Where(line => !string.IsNullOrWhiteSpace(line.Description))
            .ToArray();

        if (model.ExpectedDeliveryDate.HasValue && model.ExpectedDeliveryDate.Value < model.OrderDate)
            ModelState.AddModelError(nameof(model.ExpectedDeliveryDate), "Expected delivery cannot be earlier than the order date.");
        if (lines.Length == 0)
            ModelState.AddModelError(nameof(model.Lines), "Add at least one purchase-order line.");
        if (lines.Any(line => string.IsNullOrWhiteSpace(line.UnitSymbol) || line.Quantity <= 0 || line.UnitPrice < 0))
            ModelState.AddModelError(nameof(model.Lines), "Every purchase-order line needs a unit, positive quantity and non-negative unit price.");

        if (!ModelState.IsValid)
            return View(model);

        var payload = new
        {
            orderNumber = model.OrderNumber.Trim().ToUpperInvariant(),
            supplierId = model.SupplierId,
            projectId = model.ProjectId,
            orderDate = model.OrderDate,
            expectedDeliveryDate = model.ExpectedDeliveryDate,
            notes = model.Notes?.Trim() ?? string.Empty,
            lines = lines.Select(line => new
            {
                itemType = line.ItemType,
                catalogItemId = line.CatalogItemId,
                description = line.Description.Trim(),
                unitSymbol = line.UnitSymbol.Trim(),
                quantity = line.Quantity,
                unitPrice = line.UnitPrice
            }).ToArray()
        };

        try
        {
            await api.PostAsync<object, JsonElement>("/PurchaseOrders", payload,
                cancellationToken: cancellationToken);
            TempData["Success"] = $"Purchase order {model.OrderNumber.Trim().ToUpperInvariant()} was created.";
            return RedirectToAction(nameof(Index));
        }
        catch (ApiException exception)
        {
            logger.LogWarning(exception, "Procurement API rejected purchase order {OrderNumber}.", model.OrderNumber);
            ModelState.AddModelError(string.Empty, exception.Message);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogError(exception, "Purchase order creation could not be confirmed for {OrderNumber}.", model.OrderNumber);
            ModelState.AddModelError(string.Empty, "The service could not confirm whether the purchase order was created. Refresh the order list before retrying.");
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitOrder(Guid id, CancellationToken cancellationToken) =>
        await ChangeOrderAsync(id, "submit", "Purchase order submitted.", cancellationToken);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveOrder(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await api.PostAsync<object, JsonElement>($"/PurchaseOrders/{id:D}/approve",
                new { approvalDate = DateOnly.FromDateTime(DateTime.UtcNow) }, cancellationToken: cancellationToken);
            TempData["Success"] = "Purchase order approved.";
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            logger.LogWarning(exception, "Could not approve purchase order {PurchaseOrderId}.", id);
            TempData["Error"] = $"Approval failed: {SafeMessage(exception)}";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelOrder(Guid id, CancellationToken cancellationToken) =>
        await ChangeOrderAsync(id, "cancel", "Purchase order cancelled.", cancellationToken);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReceiveLine(
        Guid id, Guid lineId, decimal quantity, DateOnly receivedDate, CancellationToken cancellationToken)
    {
        if (quantity <= 0)
        {
            TempData["Error"] = "Receipt quantity must be greater than zero.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await api.PostAsync<object, JsonElement>($"/PurchaseOrders/{id:D}/receive-lines/{lineId:D}",
                new { quantity, receivedDate }, cancellationToken: cancellationToken);
            TempData["Success"] = "Received quantity was recorded and the linked stock ledger updated.";
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            logger.LogWarning(exception, "Could not receive line {LineId} for purchase order {PurchaseOrderId}.", lineId, id);
            TempData["Error"] = $"Receipt failed: {SafeMessage(exception)}";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> CreateDelivery(CancellationToken cancellationToken)
    {
        var model = new CreateDeliveryViewModel();
        await LoadEligibleOrdersAsync(model, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateDelivery(CreateDeliveryViewModel model, CancellationToken cancellationToken)
    {
        await LoadEligibleOrdersAsync(model, cancellationToken);
        if (!ModelState.IsValid)
            return View(model);

        try
        {
            await api.PostAsync<object, JsonElement>("/Deliveries", new
            {
                purchaseOrderId = model.PurchaseOrderId,
                deliveryNumber = model.DeliveryNumber.Trim().ToUpperInvariant(),
                expectedDate = model.ExpectedDate,
                carrier = model.Carrier.Trim(),
                trackingReference = model.TrackingReference?.Trim()
            }, cancellationToken: cancellationToken);
            TempData["Success"] = $"Delivery {model.DeliveryNumber.Trim().ToUpperInvariant()} was scheduled.";
            return RedirectToAction(nameof(Index));
        }
        catch (ApiException exception)
        {
            logger.LogWarning(exception, "Procurement API rejected delivery {DeliveryNumber}.", model.DeliveryNumber);
            ModelState.AddModelError(string.Empty, exception.Message);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogError(exception, "Delivery creation could not be confirmed for {DeliveryNumber}.", model.DeliveryNumber);
            ModelState.AddModelError(string.Empty, "The service could not confirm whether the delivery was created. Refresh the delivery list before retrying.");
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeDeliveryStatus(
        Guid id, int status, DateOnly? actualDate, CancellationToken cancellationToken)
    {
        if (status is < 0 or > 5)
        {
            TempData["Error"] = "Unknown delivery status.";
            return RedirectToAction(nameof(Index));
        }

        if (status == 3 && actualDate is null)
        {
            TempData["Error"] = "Set the actual delivery date before marking a delivery as delivered.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await api.SendAsync<JsonElement>(HttpMethod.Patch, $"/Deliveries/{id:D}/status",
                new { status, actualDate }, cancellationToken: cancellationToken);
            TempData["Success"] = "Delivery status updated.";
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            logger.LogWarning(exception, "Could not update delivery {DeliveryId}.", id);
            TempData["Error"] = $"Delivery update failed: {SafeMessage(exception)}";
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> ChangeOrderAsync(
        Guid id, string action, string successMessage, CancellationToken cancellationToken)
    {
        try
        {
            await api.PostAsync<object, JsonElement>($"/PurchaseOrders/{id:D}/{action}",
                new { }, cancellationToken: cancellationToken);
            TempData["Success"] = successMessage;
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            logger.LogWarning(exception, "Could not {Action} purchase order {PurchaseOrderId}.", action, id);
            TempData["Error"] = $"Purchase-order update failed: {SafeMessage(exception)}";
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task LoadSuppliersAsync(CreatePurchaseOrderViewModel model, CancellationToken cancellationToken)
    {
        try
        {
            var page = await api.GetAsync<ApiPage<SupplierOption>>(
                "/Suppliers?page=1&pageSize=200", cancellationToken);
            model.Suppliers = page.Items.Where(item => item.IsActive).OrderBy(item => item.Name).ToArray();
            if (model.Suppliers.Count == 0)
                model.LookupError = "No active suppliers are available. Add a supplier before creating a purchase order.";
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            logger.LogWarning(exception, "Supplier lookup failed while preparing purchase-order form.");
            model.LookupError = "Supplier data could not be loaded. Check Procurement service connectivity and your permissions.";
            model.Suppliers = Array.Empty<SupplierOption>();
        }
    }

    private async Task LoadEligibleOrdersAsync(CreateDeliveryViewModel model, CancellationToken cancellationToken)
    {
        try
        {
            var page = await api.GetAsync<ApiPage<PurchaseOrderSummary>>(
                "/PurchaseOrders?page=1&pageSize=200", cancellationToken);
            model.EligibleOrders = page.Items
                .Where(order => order.Status is 2 or 3)
                .OrderByDescending(order => order.OrderDate)
                .ToArray();
            if (model.EligibleOrders.Count == 0)
                ModelState.AddModelError(string.Empty, "No approved or partially received purchase orders are available for delivery scheduling.");
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            logger.LogWarning(exception, "Purchase-order lookup failed while preparing delivery form.");
            model.EligibleOrders = Array.Empty<PurchaseOrderSummary>();
            model.LookupError = "Purchase orders could not be loaded.";
        }
    }

    private static bool IsApiFailure(Exception exception) =>
        exception is ApiException or HttpRequestException or TaskCanceledException or JsonException;

    private static string SafeMessage(Exception exception) =>
        exception is ApiException apiException
            ? apiException.Message
            : "The service could not be reached. Refresh the page and check the service health.";
    
    private static string? Append(string? current, string next) =>
        string.IsNullOrWhiteSpace(current) ? next : current + " " + next;
}