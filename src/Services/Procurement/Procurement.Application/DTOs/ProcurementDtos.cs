using Procurement.Domain.Enums;

namespace Procurement.Application.DTOs;

public sealed record SupplierDto(Guid Id, string Code, string Name, string ContactName,
    string? Email, string? Phone, string? TaxIdentifier, string Address, int PaymentTermDays,
    string CurrencyCode, bool IsActive, DateTime CreatedDate, DateTime? LastModifiedDate);

public sealed record PurchaseOrderLineDto(Guid Id, ProcurementItemType ItemType, Guid? CatalogItemId,
    string Description, string UnitSymbol, decimal Quantity, decimal ReceivedQuantity, decimal UnitPrice, decimal LineTotal);

public sealed record PurchaseOrderDto(Guid Id, string OrderNumber, Guid SupplierId, Guid? ProjectId,
    DateOnly OrderDate, DateOnly? ExpectedDeliveryDate, DateOnly? ApprovedDate, DateOnly? CompletedDate,
    PurchaseOrderStatus Status, string Notes, decimal OrderTotal, IReadOnlyList<PurchaseOrderLineDto> Lines,
    DateTime CreatedDate, DateTime? LastModifiedDate);

public sealed record StockItemDto(Guid Id, Guid? CatalogItemId, string Sku, string Description,
    string UnitSymbol, string StorageLocation, decimal QuantityOnHand, decimal ReorderLevel,
    decimal AvailableQuantity, bool IsBelowReorderLevel, DateTime CreatedDate, DateTime? LastModifiedDate);

public sealed record StockMovementDto(Guid Id, Guid StockItemId, StockMovementType MovementType,
    decimal Quantity, string SourceDocument, Guid? ProjectId, string Notes, DateTime CreatedDate);

public sealed record SupplierDeliveryDto(Guid Id, Guid PurchaseOrderId, string DeliveryNumber,
    DateOnly ExpectedDate, DateOnly? ActualDate, string Carrier, string TrackingReference,
    DeliveryStatus Status, DateTime CreatedDate, DateTime? LastModifiedDate);