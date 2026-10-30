using Procurement.Domain.Enums;
using Shared.Common.Common;

namespace Procurement.Domain.Entities;

public sealed class SupplierDelivery : EntityBase
{
    private SupplierDelivery() { }

    public SupplierDelivery(Guid id, Guid purchaseOrderId, string deliveryNumber,
        DateOnly expectedDate, string carrier, string trackingReference)
    {
        if (id == Guid.Empty || purchaseOrderId == Guid.Empty)
            throw new ArgumentException("Delivery and order ids are required.");
        Id = id;
        PurchaseOrderId = purchaseOrderId;
        Update(deliveryNumber, expectedDate, carrier, trackingReference);
        Status = DeliveryStatus.Planned;
    }

    public Guid PurchaseOrderId { get; private set; }
    public string DeliveryNumber { get; private set; } = string.Empty;
    public DateOnly ExpectedDate { get; private set; }
    public DateOnly? ActualDate { get; private set; }
    public string Carrier { get; private set; } = string.Empty;
    public string TrackingReference { get; private set; } = string.Empty;
    public DeliveryStatus Status { get; private set; }
    public PurchaseOrder PurchaseOrder { get; private set; } = null!;

    public void Update(string deliveryNumber, DateOnly expectedDate, string carrier, string trackingReference)
    {
        if (Status is DeliveryStatus.Delivered or DeliveryStatus.Cancelled)
            throw new InvalidOperationException("Delivered or cancelled deliveries cannot be edited.");
        if (string.IsNullOrWhiteSpace(deliveryNumber) || deliveryNumber.Trim().Length > 48)
            throw new ArgumentException("Delivery number is required and limited to 48 characters.", nameof(deliveryNumber));
        if (string.IsNullOrWhiteSpace(carrier) || carrier.Trim().Length > 120)
            throw new ArgumentException("Carrier is required and limited to 120 characters.", nameof(carrier));
        if (trackingReference?.Length > 120)
            throw new ArgumentOutOfRangeException(nameof(trackingReference));

        DeliveryNumber = deliveryNumber.Trim().ToUpperInvariant();
        ExpectedDate = expectedDate;
        Carrier = carrier.Trim();
        TrackingReference = trackingReference?.Trim() ?? string.Empty;
    }

    public void ChangeStatus(DeliveryStatus status, DateOnly? actualDate = null)
    {
        if (!Enum.IsDefined(status))
            throw new ArgumentOutOfRangeException(nameof(status));
        if (Status is DeliveryStatus.Delivered or DeliveryStatus.Cancelled)
            throw new InvalidOperationException("Delivered or cancelled deliveries are terminal.");
        if (status == DeliveryStatus.Delivered && actualDate is null)
            throw new ArgumentException("Actual delivery date is required when marking a delivery as delivered.", nameof(actualDate));
        if (status != DeliveryStatus.Delivered && actualDate is not null)
            throw new ArgumentException("An actual date can only be supplied when the delivery is marked as delivered.", nameof(actualDate));

        var allowed = Status switch
        {
            DeliveryStatus.Planned => new[] { DeliveryStatus.Dispatched, DeliveryStatus.Delayed, DeliveryStatus.Cancelled },
            DeliveryStatus.Dispatched => new[] { DeliveryStatus.PartiallyReceived, DeliveryStatus.Delivered, DeliveryStatus.Delayed, DeliveryStatus.Cancelled },
            DeliveryStatus.PartiallyReceived => new[] { DeliveryStatus.Delivered, DeliveryStatus.Delayed },
            DeliveryStatus.Delayed => new[] { DeliveryStatus.Dispatched, DeliveryStatus.PartiallyReceived, DeliveryStatus.Delivered, DeliveryStatus.Cancelled },
            _ => Array.Empty<DeliveryStatus>()
        };
        if (!allowed.Contains(status))
            throw new InvalidOperationException($"Delivery cannot transition from {Status} to {status}.");

        Status = status;
        if (status == DeliveryStatus.Delivered)
            ActualDate = actualDate;
    }
}
