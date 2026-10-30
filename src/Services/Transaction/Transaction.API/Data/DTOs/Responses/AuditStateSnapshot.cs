using Transaction.API.Entities;

namespace Transaction.API.Data.DTOs.Responses;

/// <summary>
/// Versioned, portable backup of the append-only analytics event store.
/// Import never replaces or deletes existing events.
/// </summary>
public sealed class AuditStateSnapshot
{
    public int SchemaVersion { get; init; } = 1;
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public IReadOnlyList<AuditRecord> Records { get; init; } = Array.Empty<AuditRecord>();
}

public sealed record AuditStateImportResult(
    int SchemaVersion, int Received, int Inserted, int SkippedExisting, DateTime ImportedAtUtc);
