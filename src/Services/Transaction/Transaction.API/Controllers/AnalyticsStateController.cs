using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Transaction.API.Data.DTOs.Responses;
using Transaction.API.Entities;
using Transaction.API.Repositories.Interfaces;

namespace Transaction.API.Controllers;

/// <summary>
/// Administrator-only, versioned backup and restore for the append-only analytics event store.
/// Restore is additive: an existing event ID is never overwritten.
/// </summary>
[ApiController]
[Route("api/v1/analytics/state")]
[Authorize(Roles = "Administrator")]
public sealed class AnalyticsStateController(
    IAuditEventRepository repository,
    ILogger<AnalyticsStateController> logger) : ControllerBase
{
    private const int SchemaVersion = 1;
    private const int MaximumRecords = 50_000;
    private const int MaximumMetadataEntries = 100;
    private static readonly string[] SensitiveMetadataTerms =
        ["password", "secret", "token", "authorization", "credential", "email", "phone", "address", "fullname", "full_name"];

    [HttpGet]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> Export(CancellationToken cancellationToken)
    {
        var records = await repository.GetSnapshotAsync(MaximumRecords + 1, cancellationToken);
        if (records.Count > MaximumRecords)
            return Problem(statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "Analytics snapshot is too large",
                detail: $"The snapshot exceeds the {MaximumRecords:N0}-event export limit. Use filtered CSV exports instead.");

        var snapshot = new AuditStateSnapshot
        {
            SchemaVersion = SchemaVersion,
            CreatedAtUtc = DateTime.UtcNow,
            Records = records
        };
        var json = JsonSerializer.SerializeToUtf8Bytes(snapshot, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        });
        logger.LogInformation("Created analytics state snapshot containing {RecordCount} events", records.Count);
        return File(json, "application/json; charset=utf-8",
            $"landscape-analytics-state-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json");
    }

    [HttpPost]
    [Consumes("application/json")]
    [RequestSizeLimit(50_000_000)]
    [ProducesResponseType(typeof(AuditStateImportResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<ActionResult<AuditStateImportResult>> Import(
        [FromBody] AuditStateSnapshot? snapshot, CancellationToken cancellationToken)
    {
        if (snapshot is null)
            return BadRequest(new ProblemDetails { Title = "Snapshot is required." });
        if (snapshot.SchemaVersion != SchemaVersion)
            return BadRequest(new ProblemDetails
            {
                Title = "Unsupported snapshot version",
                Detail = $"Expected schema version {SchemaVersion}; received {snapshot.SchemaVersion}."
            });
        if (snapshot.Records is null)
            return BadRequest(new ProblemDetails { Title = "Snapshot records are required." });
        if (snapshot.Records.Count > MaximumRecords)
            return StatusCode(StatusCodes.Status413PayloadTooLarge,
                new ProblemDetails { Title = "Snapshot record limit exceeded", Detail = $"At most {MaximumRecords:N0} records can be imported." });

        var validation = ValidateRecords(snapshot.Records);
        if (validation is not null)
            return BadRequest(new ProblemDetails { Title = "Invalid analytics snapshot", Detail = validation });

        var inserted = await repository.ImportMissingAsync(snapshot.Records, cancellationToken);
        var result = new AuditStateImportResult(
            SchemaVersion, snapshot.Records.Count, inserted,
            snapshot.Records.Count - inserted, DateTime.UtcNow);
        logger.LogInformation(
            "Imported analytics state snapshot: received {Received}, inserted {Inserted}, already present {Skipped}",
            result.Received, result.Inserted, result.SkippedExisting);
        return Ok(result);
    }

    private static string? ValidateRecords(IReadOnlyList<AuditRecord> records)
    {
        var ids = new HashSet<Guid>();
        for (var index = 0; index < records.Count; index++)
        {
            var record = records[index];
            if (record is null) return $"Record at index {index} is null.";
            if (record.Id == Guid.Empty) return $"Record at index {index} has an empty event ID.";
            if (!ids.Add(record.Id)) return $"Duplicate event ID {record.Id:D} appears in the snapshot.";
            if (record.OccurredAtUtc == default) return $"Record {record.Id:D} has no occurrence timestamp.";
            if (string.IsNullOrWhiteSpace(record.SourceService) || record.SourceService.Length > 100)
                return $"Record {record.Id:D} has an invalid source service.";
            if (string.IsNullOrWhiteSpace(record.Action) || record.Action.Length > 100)
                return $"Record {record.Id:D} has an invalid action.";
            if (string.IsNullOrWhiteSpace(record.EntityType) || record.EntityType.Length > 100)
                return $"Record {record.Id:D} has an invalid entity type.";
            if (record.EntityId?.Length > 128 || record.ActorId?.Length > 128 || record.CorrelationId?.Length > 128)
                return $"Record {record.Id:D} contains an identifier longer than 128 characters.";
            if (record.Metadata is null || record.Metadata.Count > MaximumMetadataEntries)
                return $"Record {record.Id:D} has invalid metadata.";
            foreach (var item in record.Metadata)
            {
                if (string.IsNullOrWhiteSpace(item.Key) || item.Key.Length > 100 || item.Value is null || item.Value.Length > 500)
                    return $"Record {record.Id:D} contains invalid metadata.";
                if (SensitiveMetadataTerms.Any(term => item.Key.Contains(term, StringComparison.OrdinalIgnoreCase)))
                    return $"Record {record.Id:D} contains a prohibited sensitive metadata key.";
            }
        }

        return null;
    }
}
