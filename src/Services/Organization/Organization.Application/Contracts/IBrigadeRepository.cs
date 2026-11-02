using Organization.Domain.Entities;
using Shared.Common.Persistence;

namespace Organization.Application.Contracts;

public interface IBrigadeRepository
{
    Task<PagedResult<Brigade>> GetBrigadesAsync(int page, int pageSize, bool includeInactive, CancellationToken cancellationToken);
    Task<Brigade?> GetByIdWithMembersAsync(Guid id, bool tracking, CancellationToken cancellationToken);
    Task AddAsync(Brigade brigade, CancellationToken cancellationToken);
    Task SaveAsync(Brigade brigade, CancellationToken cancellationToken);
}
