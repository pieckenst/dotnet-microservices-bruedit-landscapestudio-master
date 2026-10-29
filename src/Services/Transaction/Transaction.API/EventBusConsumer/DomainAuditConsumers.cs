using EventBus.Messages.Events;
using MassTransit;
using Transaction.API.Entities;
using Transaction.API.Repositories.Interfaces;
using System.Globalization;

namespace Transaction.API.EventBusConsumer;

internal static class DomainAuditRecordFactory
{
    public static AuditRecord Create(
        IntegrationBaseEvent message,
        string sourceService,
        string action,
        string entityType,
        Guid? entityId,
        Guid? actorId,
        DateTime occurredAtUtc,
        Dictionary<string, string>? metadata = null)
    {
        var occurred = occurredAtUtc == default ? message.CreationDate : occurredAtUtc;
        occurred = occurred.Kind switch
        {
            DateTimeKind.Utc => occurred,
            DateTimeKind.Local => occurred.ToUniversalTime(),
            _ => DateTime.SpecifyKind(occurred, DateTimeKind.Utc)
        };

        return new AuditRecord
        {
            Id = message.Id,
            OccurredAtUtc = occurred,
            SourceService = sourceService,
            Action = action,
            EntityType = entityType,
            EntityId = entityId?.ToString("D"),
            ActorId = actorId?.ToString("D"),
            CorrelationId = null,
            Metadata = metadata ?? new Dictionary<string, string>(StringComparer.Ordinal)
        };
    }

    public static string Id(Guid id) => id.ToString("D");
    public static string Number(decimal value) => value.ToString(CultureInfo.InvariantCulture);
    public static string Date(DateTime? value) => value?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) ?? string.Empty;
}

public sealed class UserRegisteredAuditConsumer(IAuditEventRepository repository) : IConsumer<UserRegisteredEvent>
{
    public Task Consume(ConsumeContext<UserRegisteredEvent> context)
    {
        var message = context.Message;
        if (message.UserId == Guid.Empty) return Task.CompletedTask;
        var record = DomainAuditRecordFactory.Create(message, "Identity", "UserRegistered", "IdentityUser",
            message.UserId, message.UserId, message.RegisteredAt,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["role"] = Safe(message.RoleName, 50)
            });
        return repository.AddAsync(record, context.CancellationToken);
    }

    private static string Safe(string value, int limit) =>
        string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim()[..Math.Min(value.Trim().Length, limit)];
}

public sealed class ProfileUpdatedAuditConsumer(IAuditEventRepository repository) : IConsumer<ProfileUpdatedEvent>
{
    public Task Consume(ConsumeContext<ProfileUpdatedEvent> context)
    {
        var message = context.Message;
        if (message.UserId == Guid.Empty) return Task.CompletedTask;
        var record = DomainAuditRecordFactory.Create(message, "Account", "ProfileUpdated", "AccountProfile",
            message.UserId, message.UserId, message.UpdatedAt,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["updatedFields"] = string.IsNullOrWhiteSpace(message.UpdatedFields) ? "profile" : message.UpdatedFields[..Math.Min(message.UpdatedFields.Length, 200)]
            });
        return repository.AddAsync(record, context.CancellationToken);
    }
}

public sealed class ProjectStageCompletedAuditConsumer(IAuditEventRepository repository) : IConsumer<ProjectStageCompletedEvent>
{
    public Task Consume(ConsumeContext<ProjectStageCompletedEvent> context)
    {
        var message = context.Message;
        if (message.ProjectId == Guid.Empty || message.StageId == Guid.Empty) return Task.CompletedTask;
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["projectId"] = DomainAuditRecordFactory.Id(message.ProjectId),
            ["completedTaskCount"] = message.CompletedTaskCount.ToString(CultureInfo.InvariantCulture),
            ["totalTaskCount"] = message.TotalTaskCount.ToString(CultureInfo.InvariantCulture),
            ["projectStatus"] = message.ProjectStatus.ToString(),
            ["stageName"] = string.IsNullOrWhiteSpace(message.StageName) ? "unknown" : message.StageName.Trim()[..Math.Min(message.StageName.Trim().Length, 100)]
        };
        return repository.AddAsync(DomainAuditRecordFactory.Create(message, "Project", "StageCompleted",
            "ProjectStage", message.StageId, null, message.CompletedDate, metadata), context.CancellationToken);
    }
}

public sealed class EmployeeAssignedAuditConsumer(IAuditEventRepository repository) : IConsumer<EmployeeAssignedEvent>
{
    public Task Consume(ConsumeContext<EmployeeAssignedEvent> context)
    {
        var message = context.Message;
        if (message.EmployeeId == Guid.Empty || message.ProjectId == Guid.Empty) return Task.CompletedTask;
        // Deliberately exclude EmployeeFullName: analytics needs identifiers and assignment facts, not full personal names.
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["projectId"] = DomainAuditRecordFactory.Id(message.ProjectId),
            ["brigadeId"] = message.BrigadeId?.ToString("D") ?? string.Empty,
            ["assignmentRole"] = string.IsNullOrWhiteSpace(message.AssignmentRole) ? "unspecified" : message.AssignmentRole.Trim()[..Math.Min(message.AssignmentRole.Trim().Length, 100)],
            ["isResponsible"] = message.IsResponsible.ToString(CultureInfo.InvariantCulture)
        };
        return repository.AddAsync(DomainAuditRecordFactory.Create(message, "Organization", "EmployeeAssigned",
            "EmployeeAssignment", message.EmployeeId, null, message.AssignedDate, metadata), context.CancellationToken);
    }
}

public sealed class PurchaseOrderCreatedAuditConsumer(IAuditEventRepository repository) : IConsumer<PurchaseOrderCreatedEvent>
{
    public Task Consume(ConsumeContext<PurchaseOrderCreatedEvent> context)
    {
        var message = context.Message;
        if (message.PurchaseOrderId == Guid.Empty) return Task.CompletedTask;
        // SupplierName is intentionally excluded; the stable supplier identifier is enough for analytics.
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["supplierId"] = DomainAuditRecordFactory.Id(message.SupplierId),
            ["orderNumber"] = Safe(message.OrderNumber, 80),
            ["totalAmount"] = DomainAuditRecordFactory.Number(message.TotalAmount),
            ["currency"] = Safe(message.Currency, 8),
            ["expectedDeliveryDate"] = DomainAuditRecordFactory.Date(message.ExpectedDeliveryDate)
        };
        return repository.AddAsync(DomainAuditRecordFactory.Create(message, "Procurement", "PurchaseOrderCreated",
            "PurchaseOrder", message.PurchaseOrderId, null, message.OrderDate, metadata), context.CancellationToken);
    }

    private static string Safe(string value, int limit) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim()[..Math.Min(value.Trim().Length, limit)];
}

public sealed class EstimateApprovedAuditConsumer(IAuditEventRepository repository) : IConsumer<EstimateApprovedEvent>
{
    public Task Consume(ConsumeContext<EstimateApprovedEvent> context)
    {
        var message = context.Message;
        if (message.EstimateId == Guid.Empty) return Task.CompletedTask;
        // ClientName and ProjectName are excluded; analytics keys records by immutable identifiers.
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["projectId"] = DomainAuditRecordFactory.Id(message.ProjectId),
            ["clientId"] = DomainAuditRecordFactory.Id(message.ClientId),
            ["estimatedTotal"] = DomainAuditRecordFactory.Number(message.EstimatedTotal),
            ["materialsCost"] = DomainAuditRecordFactory.Number(message.MaterialsCost),
            ["worksCost"] = DomainAuditRecordFactory.Number(message.WorksCost),
            ["currency"] = string.IsNullOrWhiteSpace(message.Currency) ? "BYN" : message.Currency.Trim()[..Math.Min(message.Currency.Trim().Length, 8)],
            ["lineItemCount"] = message.LineItemCount.ToString(CultureInfo.InvariantCulture)
        };
        return repository.AddAsync(DomainAuditRecordFactory.Create(message, "Project", "EstimateApproved",
            "Estimate", message.EstimateId, null, message.ApprovedDate, metadata), context.CancellationToken);
    }
}

public sealed class MaterialReceivedAuditConsumer(IAuditEventRepository repository) : IConsumer<MaterialReceivedEvent>
{
    public Task Consume(ConsumeContext<MaterialReceivedEvent> context)
    {
        var message = context.Message;
        if (message.MaterialReceiptId == Guid.Empty || message.PurchaseOrderId == Guid.Empty) return Task.CompletedTask;
        // MaterialName, UnitName and ReceivedBy are deliberately omitted from persistent audit metadata.
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["purchaseOrderId"] = DomainAuditRecordFactory.Id(message.PurchaseOrderId),
            ["materialId"] = DomainAuditRecordFactory.Id(message.MaterialId),
            ["unitSymbol"] = string.IsNullOrWhiteSpace(message.UnitSymbol) ? "unspecified" : message.UnitSymbol.Trim()[..Math.Min(message.UnitSymbol.Trim().Length, 24)],
            ["quantityReceived"] = DomainAuditRecordFactory.Number(message.QuantityReceived),
            ["unitCost"] = DomainAuditRecordFactory.Number(message.UnitCost),
            ["totalCost"] = DomainAuditRecordFactory.Number(message.TotalCost)
        };
        return repository.AddAsync(DomainAuditRecordFactory.Create(message, "Procurement", "MaterialReceived",
            "MaterialReceipt", message.MaterialReceiptId, null, message.ReceivedDate, metadata), context.CancellationToken);
    }
}
