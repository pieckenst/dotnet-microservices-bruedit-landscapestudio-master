namespace EventBus.Messages.Events;

/// <summary>
/// A minimal, non-sensitive integration event used for cross-service audit trails.
/// Do not place credentials, tokens, payment data, or full personal profiles in Metadata.
/// </summary>
public sealed class AuditEvent : IntegrationBaseEvent
{
    public string SourceService { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? ActorId { get; set; }
    public string? CorrelationId { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}
