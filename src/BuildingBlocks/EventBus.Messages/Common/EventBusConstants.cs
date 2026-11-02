namespace EventBus.Messages.Common
{
    public static class EventBusConstants
    {
        public const string AuditEventQueue = "audit-events-queue";

        // Landscape Studio integration event queues
        public const string ProjectStageCompletedQueue = "projectstage-completed-queue";
        public const string EmployeeAssignedQueue = "employee-assigned-queue";
        public const string PurchaseOrderCreatedQueue = "purchaseorder-created-queue";
        public const string EstimateApprovedQueue = "estimate-approved-queue";
        public const string MaterialReceivedQueue = "material-received-queue";
        public const string UserRegisteredQueue = "user-registered-queue";
        public const string ProfileUpdatedQueue = "profile-updated-queue";
    }
}
