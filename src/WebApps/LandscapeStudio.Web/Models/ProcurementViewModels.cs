using System.ComponentModel.DataAnnotations;

namespace LandscapeStudio.Web.Models;

public sealed record SupplierOption(Guid Id, string Code, string Name, string CurrencyCode, bool IsActive);
public sealed record PurchaseOrderLineSummary(
    Guid Id, int ItemType, Guid? CatalogItemId, string Description, string UnitSymbol,
    decimal Quantity, decimal ReceivedQuantity, decimal UnitPrice, decimal LineTotal);
public sealed record PurchaseOrderSummary(
    Guid Id, string OrderNumber, Guid SupplierId, Guid? ProjectId, DateOnly OrderDate,
    DateOnly? ExpectedDeliveryDate, DateOnly? ApprovedDate, DateOnly? CompletedDate,
    int Status, string Notes, decimal OrderTotal, IReadOnlyList<PurchaseOrderLineSummary> Lines,
    DateTime CreatedDate, DateTime? LastModifiedDate);
public sealed record StockItemSummary(
    Guid Id, Guid? CatalogItemId, string Sku, string Description, string UnitSymbol,
    string StorageLocation, decimal QuantityOnHand, decimal ReorderLevel, decimal AvailableQuantity,
    bool IsBelowReorderLevel);
public sealed record SupplierDeliverySummary(
    Guid Id, Guid PurchaseOrderId, string DeliveryNumber, DateOnly ExpectedDate,
    DateOnly? ActualDate, string Carrier, string TrackingReference, int Status);

public sealed class ProcurementIndexViewModel
{
    public IReadOnlyList<PurchaseOrderSummary> Orders { get; set; } = Array.Empty<PurchaseOrderSummary>();
    public IReadOnlyList<StockItemSummary> StockItems { get; set; } = Array.Empty<StockItemSummary>();
    public IReadOnlyList<SupplierDeliverySummary> Deliveries { get; set; } = Array.Empty<SupplierDeliverySummary>();
    public string? Error { get; set; }
}

public sealed class CreatePurchaseOrderViewModel
{
    [Required, StringLength(40, MinimumLength = 1)]
    public string OrderNumber { get; set; } = string.Empty;

    [Required]
    public Guid SupplierId { get; set; }

    public Guid? ProjectId { get; set; }

    [Required]
    public DateOnly OrderDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    public DateOnly? ExpectedDeliveryDate { get; set; }

    [StringLength(4000)]
    public string? Notes { get; set; }

    public List<CreatePurchaseOrderLineViewModel> Lines { get; set; } = [new()];

    public IReadOnlyList<SupplierOption> Suppliers { get; set; } = Array.Empty<SupplierOption>();
    public string? LookupError { get; set; }
}

public sealed class CreatePurchaseOrderLineViewModel
{
    [Range(0, 4)]
    public int ItemType { get; set; }

    public Guid? CatalogItemId { get; set; }

    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    [StringLength(24)]
    public string UnitSymbol { get; set; } = "pcs";

    [Range(typeof(decimal), "0.001", "1000000000")]
    public decimal Quantity { get; set; } = 1m;

    [Range(typeof(decimal), "0", "1000000000000")]
    public decimal UnitPrice { get; set; }
}

public sealed class CreateDeliveryViewModel
{
    [Required]
    public Guid PurchaseOrderId { get; set; }

    [Required, StringLength(48, MinimumLength = 1)]
    public string DeliveryNumber { get; set; } = string.Empty;

    [Required]
    public DateOnly ExpectedDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));

    [Required, StringLength(120, MinimumLength = 1)]
    public string Carrier { get; set; } = string.Empty;

    [StringLength(120)]
    public string? TrackingReference { get; set; }

    public IReadOnlyList<PurchaseOrderSummary> EligibleOrders { get; set; } = Array.Empty<PurchaseOrderSummary>();
}