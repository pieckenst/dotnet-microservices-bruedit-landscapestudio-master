using EventBus.Messages.Common;

namespace EventBus.Messages.Events
{
    /// <summary>
    /// Published when an estimate is approved by the client.
    /// Consumed by Analytics Service for revenue and cost tracking.
    /// </summary>
    public class EstimateApprovedEvent : IntegrationBaseEvent
    {
        public Guid EstimateId { get; set; }
        public Guid ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public Guid ClientId { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public decimal EstimatedTotal { get; set; }
        public decimal MaterialsCost { get; set; }
        public decimal WorksCost { get; set; }
        public string Currency { get; set; } = "RUB";
        public DateTime ApprovedDate { get; set; }
        public int LineItemCount { get; set; }
    }
}
