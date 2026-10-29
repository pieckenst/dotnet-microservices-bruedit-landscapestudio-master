namespace Transaction.API.Data.DTOs.Responses;

public sealed record AuditEventDto(
    Guid Id, DateTime OccurredAtUtc, string SourceService, string Action, string EntityType,
    string? EntityId, string? ActorId, string? CorrelationId, IReadOnlyDictionary<string, string> Metadata);

public sealed record PagedAuditEventsResponse(
    IReadOnlyList<AuditEventDto> Items, int Page, int PageSize, long TotalCount, int TotalPages);

public sealed record AnalyticsCountDto(string Name, long Count);

public sealed record AnalyticsSummaryDto(
    DateTime FromUtc, DateTime ToUtc, long TotalEvents,
    IReadOnlyList<AnalyticsCountDto> ByService,
    IReadOnlyList<AnalyticsCountDto> ByAction);

internal static class AuditEventMapping
{
    public static AuditEventDto ToDto(Entities.AuditRecord record) => new(
        record.Id, DateTime.SpecifyKind(record.OccurredAtUtc, DateTimeKind.Utc), record.SourceService,
        record.Action, record.EntityType, record.EntityId, record.ActorId, record.CorrelationId,
        new Dictionary<string, string>(record.Metadata, StringComparer.Ordinal));
}
