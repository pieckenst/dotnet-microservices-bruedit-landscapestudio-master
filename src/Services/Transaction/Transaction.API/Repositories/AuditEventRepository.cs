using MongoDB.Driver;
using Transaction.API.Data.DTOs.Requests;
using Transaction.API.Data.Interfaces;
using Transaction.API.Entities;
using Transaction.API.Repositories.Interfaces;

namespace Transaction.API.Repositories;

public sealed class AuditEventRepository(IAuditEventContext context) : IAuditEventRepository
{
    private FilterDefinition<AuditRecord> BuildFilter(AuditEventQuery query)
    {
        var f = Builders<AuditRecord>.Filter;
        var filters = new List<FilterDefinition<AuditRecord>>();
        if (query.FromUtc.HasValue) filters.Add(f.Gte(x => x.OccurredAtUtc, AsUtc(query.FromUtc.Value)));
        if (query.ToUtc.HasValue) filters.Add(f.Lte(x => x.OccurredAtUtc, AsUtc(query.ToUtc.Value)));
        AddExact(filters, f, x => x.SourceService, query.SourceService);
        AddExact(filters, f, x => x.Action, query.Action);
        AddExact(filters, f, x => x.EntityType, query.EntityType);
        AddExact(filters, f, x => x.EntityId, query.EntityId);
        AddExact(filters, f, x => x.ActorId, query.ActorId);
        return filters.Count == 0 ? f.Empty : f.And(filters);
    }

    private static void AddExact<TField>(
        ICollection<FilterDefinition<AuditRecord>> filters,
        FilterDefinitionBuilder<AuditRecord> builder,
        System.Linq.Expressions.Expression<Func<AuditRecord, TField>> field,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) filters.Add(builder.Eq(field, value.Trim()));
    }

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    public async Task AddAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        try
        {
            await context.AuditEvents.InsertOneAsync(record, cancellationToken: cancellationToken);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // A redelivered integration message is expected to be idempotent.
        }
    }

    public async Task<(IReadOnlyList<AuditRecord> Items, long TotalCount)> QueryAsync(
        AuditEventQuery query, CancellationToken cancellationToken = default)
    {
        var filter = BuildFilter(query);
        var total = await context.AuditEvents.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await context.AuditEvents.Find(filter)
            .SortByDescending(x => x.OccurredAtUtc)
            .Skip(checked((query.Page - 1) * query.PageSize))
            .Limit(query.PageSize)
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<IReadOnlyList<AuditRecord>> GetForExportAsync(
        AuditEventQuery query, int maximumRows, CancellationToken cancellationToken = default) =>
        await context.AuditEvents.Find(BuildFilter(query))
            .SortByDescending(x => x.OccurredAtUtc)
            .Limit(maximumRows)
            .ToListAsync(cancellationToken);

    public Task<long> CountAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default) =>
        context.AuditEvents.CountDocumentsAsync(
            Builders<AuditRecord>.Filter.Gte(x => x.OccurredAtUtc, AsUtc(fromUtc)) &
            Builders<AuditRecord>.Filter.Lte(x => x.OccurredAtUtc, AsUtc(toUtc)),
            cancellationToken: cancellationToken);

    public async Task<IReadOnlyList<(string Name, long Count)>> CountByServiceAsync(
        DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var filter = DateFilter(fromUtc, toUtc);
        var rows = await context.AuditEvents.Aggregate()
            .Match(filter)
            .Group(x => x.SourceService, g => new { Name = g.Key, Count = g.LongCount() })
            .SortByDescending(x => x.Count)
            .ToListAsync(cancellationToken);
        return rows.Select(x => (x.Name, x.Count)).ToArray();
    }

    public async Task<IReadOnlyList<(string Name, long Count)>> CountByActionAsync(
        DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var filter = DateFilter(fromUtc, toUtc);
        var rows = await context.AuditEvents.Aggregate()
            .Match(filter)
            .Group(x => x.Action, g => new { Name = g.Key, Count = g.LongCount() })
            .SortByDescending(x => x.Count)
            .ToListAsync(cancellationToken);
        return rows.Select(x => (x.Name, x.Count)).ToArray();
    }

    private static FilterDefinition<AuditRecord> DateFilter(DateTime fromUtc, DateTime toUtc) =>
        Builders<AuditRecord>.Filter.Gte(x => x.OccurredAtUtc, AsUtc(fromUtc)) &
        Builders<AuditRecord>.Filter.Lte(x => x.OccurredAtUtc, AsUtc(toUtc));
}
