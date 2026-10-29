using EventBus.Messages.Common;

namespace EventBus.Messages.Events
{
    /// <summary>
    /// Published when a purchase order is created in the Procurement Service.
    /// Consumed by Analytics Service for procurement tracking.
    /// </summary>
    public class PurchaseOrderCreatedEvent : IntegrationBaseEvent
    {
        public Guid PurchaseOrderId { get; set; }
        public Guid SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string OrderNumber { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string Currency { get; set; } = "RUB";
        public DateTime OrderDate { get; set; }
        public DateTime? ExpectedDeliveryDate { get; set; }
    }
}
