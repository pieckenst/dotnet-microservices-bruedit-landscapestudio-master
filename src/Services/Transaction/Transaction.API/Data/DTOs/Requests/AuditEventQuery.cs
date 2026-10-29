namespace Transaction.API.Data.DTOs.Requests;

/// <summary>Validated query filters for searching the immutable audit log.</summary>
public sealed class AuditEventQuery
{
    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtc { get; init; }
    public string? SourceService { get; init; }
    public string? Action { get; init; }
    public string? EntityType { get; init; }
    public string? EntityId { get; init; }
    public string? ActorId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;

    public string? Validate()
    {
        if (Page < 1) return "Page must be greater than zero.";
        if (PageSize is < 1 or > 200) return "PageSize must be between 1 and 200.";
        if (FromUtc.HasValue && ToUtc.HasValue && FromUtc.Value > ToUtc.Value)
            return "FromUtc cannot be later than ToUtc.";
        if (SourceService?.Length > 100) return "SourceService cannot exceed 100 characters.";
        if (Action?.Length > 100) return "Action cannot exceed 100 characters.";
        if (EntityType?.Length > 100) return "EntityType cannot exceed 100 characters.";
        if (EntityId?.Length > 128) return "EntityId cannot exceed 128 characters.";
        if (ActorId?.Length > 128) return "ActorId cannot exceed 128 characters.";
        return null;
    }
}
