using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Transaction.API.Entities;

[BsonIgnoreExtraElements]
public sealed class AuditRecord
{
    [BsonId]
    public Guid Id { get; set; }
    [BsonElement("occurredAtUtc")]
    public DateTime OccurredAtUtc { get; set; }
    [BsonElement("sourceService")]
    public string SourceService { get; set; } = string.Empty;
    [BsonElement("action")]
    public string Action { get; set; } = string.Empty;
    [BsonElement("entityType")]
    public string EntityType { get; set; } = string.Empty;
    [BsonElement("entityId")]
    public string? EntityId { get; set; }
    [BsonElement("actorId")]
    public string? ActorId { get; set; }
    [BsonElement("correlationId")]
    public string? CorrelationId { get; set; }
    [BsonElement("metadata")]
    public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.Ordinal);
}
