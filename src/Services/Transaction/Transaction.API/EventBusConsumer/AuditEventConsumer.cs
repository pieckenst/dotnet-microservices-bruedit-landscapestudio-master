using EventBus.Messages.Events;
using MassTransit;
using Transaction.API.Entities;
using Transaction.API.Repositories.Interfaces;

namespace Transaction.API.EventBusConsumer;

public sealed class AuditEventConsumer(
    ILogger<AuditEventConsumer> logger,
    IAuditEventRepository repository) : IConsumer<AuditEvent>
{
    private static readonly string[] SensitiveMetadataTerms =
        ["password", "secret", "token", "authorization", "credential", "email", "phone", "address", "fullname", "full_name"];

    public async Task Consume(ConsumeContext<AuditEvent> context)
    {
        var message = context.Message;
        if (message.Id == Guid.Empty ||
            string.IsNullOrWhiteSpace(message.SourceService) ||
            string.IsNullOrWhiteSpace(message.Action) ||
            string.IsNullOrWhiteSpace(message.EntityType))
        {
            logger.LogWarning("Ignoring malformed audit event {MessageId}", message.Id);
            return;
        }

        var record = new AuditRecord
        {
            Id = message.Id,
            OccurredAtUtc = message.CreationDate.Kind == DateTimeKind.Utc
                ? message.CreationDate
                : DateTime.SpecifyKind(message.CreationDate, DateTimeKind.Utc),
            SourceService = Limit(message.SourceService.Trim(), 100),
            Action = Limit(message.Action.Trim(), 100),
            EntityType = Limit(message.EntityType.Trim(), 100),
            EntityId = Normalize(message.EntityId),
            ActorId = Normalize(message.ActorId),
            CorrelationId = Normalize(message.CorrelationId) ?? context.CorrelationId?.ToString(),
            Metadata = NormalizeMetadata(message.Metadata)
        };

        await repository.AddAsync(record, context.CancellationToken);
        logger.LogDebug("Stored audit event {EventId} from {SourceService}", record.Id, record.SourceService);
    }

    private static Dictionary<string, string> NormalizeMetadata(IReadOnlyDictionary<string, string>? metadata)
    {
        var normalized = new Dictionary<string, string>(StringComparer.Ordinal);
        if (metadata is null) return normalized;
        foreach (var item in metadata.Take(100))
        {
            var key = item.Key?.Trim();
            var value = item.Value;
            if (string.IsNullOrWhiteSpace(key) || value is null ||
                SensitiveMetadataTerms.Any(term => key.Contains(term, StringComparison.OrdinalIgnoreCase)))
                continue;
            key = Limit(key, 100);
            if (normalized.ContainsKey(key)) continue;
            normalized.Add(key, Limit(value, 500));
        }
        return normalized;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : Limit(value.Trim(), 128);

    private static string Limit(string value, int maximum) =>
        value.Length <= maximum ? value : value[..maximum];
}
