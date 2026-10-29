using Transaction.API.Data.DTOs.Requests;
using Transaction.API.Entities;

namespace Transaction.API.Repositories.Interfaces;

public interface IAuditEventRepository
{
    Task AddAsync(AuditRecord record, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<AuditRecord> Items, long TotalCount)> QueryAsync(AuditEventQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditRecord>> GetForExportAsync(AuditEventQuery query, int maximumRows, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<(string Name, long Count)>> CountByServiceAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<(string Name, long Count)>> CountByActionAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
    Task<long> CountAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
}
