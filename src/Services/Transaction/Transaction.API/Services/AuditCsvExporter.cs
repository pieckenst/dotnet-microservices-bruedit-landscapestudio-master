using System.Text;
using Transaction.API.Entities;

namespace Transaction.API.Services;

/// <summary>CSV output deliberately excludes metadata to avoid accidental personal-data export.</summary>
public static class AuditCsvExporter
{
    private static readonly string[] Headers =
    [
        "event_id", "occurred_at_utc", "source_service", "action",
        "entity_type", "entity_id", "actor_id", "correlation_id"
    ];

    public static string Export(IEnumerable<AuditRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        var output = new StringBuilder();
        output.AppendLine(string.Join(",", Headers.Select(Escape)));
        foreach (var record in records)
        {
            output.AppendLine(string.Join(",", new[]
            {
                record.Id.ToString("D"),
                record.OccurredAtUtc.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                record.SourceService,
                record.Action,
                record.EntityType,
                record.EntityId ?? string.Empty,
                record.ActorId ?? string.Empty,
                record.CorrelationId ?? string.Empty
            }.Select(Escape)));
        }
        return output.ToString();
    }

    /// <summary>
    /// Exports the same privacy-minimized columns as CSV using an escaped pipe-delimited format.
    /// Backslash, pipe and line breaks are escaped so one event always occupies one physical line.
    /// </summary>
    public static string ExportPipe(IEnumerable<AuditRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        var output = new StringBuilder();
        output.AppendLine(string.Join("|", Headers.Select(EscapePipe)));
        foreach (var record in records)
        {
            output.AppendLine(string.Join("|", new[]
            {
                record.Id.ToString("D"),
                record.OccurredAtUtc.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                record.SourceService,
                record.Action,
                record.EntityType,
                record.EntityId ?? string.Empty,
                record.ActorId ?? string.Empty,
                record.CorrelationId ?? string.Empty
            }.Select(EscapePipe)));
        }
        return output.ToString();
    }

    private static string EscapePipe(string? value)
    {
        value ??= string.Empty;
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
            value = "'" + value;
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }

    private static string Escape(string? value)
    {
        value ??= string.Empty;
        // Spreadsheet applications may execute CSV cells beginning with formula prefixes.
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
            value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
