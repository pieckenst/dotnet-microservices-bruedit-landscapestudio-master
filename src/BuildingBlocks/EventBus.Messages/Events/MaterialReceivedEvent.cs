using EventBus.Messages.Common;

namespace EventBus.Messages.Events
{
    /// <summary>
    /// Published when materials are received into stock.
    /// Consumed by Analytics Service for inventory tracking.
    /// </summary>
    public class MaterialReceivedEvent : IntegrationBaseEvent
    {
        public Guid MaterialReceiptId { get; set; }
        public Guid PurchaseOrderId { get; set; }
        public Guid MaterialId { get; set; }
        public string MaterialName { get; set; } = string.Empty;
        public Guid MeasurementUnitId { get; set; }
        public string UnitName { get; set; } = string.Empty;
        public decimal QuantityReceived { get; set; }
        public decimal UnitCost { get; set; }
        public decimal TotalCost { get; set; }
        public DateTime ReceivedDate { get; set; }
        public string ReceivedBy { get; set; } = string.Empty;
    }
}
