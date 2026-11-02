using Microsoft.EntityFrameworkCore;
using Organization.Application.Contracts;
using Organization.Domain.Entities;
using Organization.Infrastructure.Persistence;
using Shared.Common.Persistence;

namespace Organization.Infrastructure.Repositories;

public sealed class BrigadeRepository(OrganizationContext context) : IBrigadeRepository
{
    public async Task<PagedResult<Brigade>> GetBrigadesAsync(
        int page, int pageSize, bool includeInactive, CancellationToken cancellationToken)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize is < 1 or > 200) throw new ArgumentOutOfRangeException(nameof(pageSize));
        var query = context.Brigades.Include(brigade => brigade.Members).ThenInclude(member => member.Employee)
            .AsNoTracking().Where(brigade => includeInactive || brigade.IsActive);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(brigade => brigade.Code).Skip((page - 1) * pageSize)
            .Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<Brigade>(items, page, pageSize, total);
    }

    public async Task<Brigade?> GetByIdWithMembersAsync(Guid id, bool tracking, CancellationToken cancellationToken)
    {
        IQueryable<Brigade> query = context.Brigades.Include(brigade => brigade.Members).ThenInclude(member => member.Employee);
        if (!tracking) query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(brigade => brigade.Id == id, cancellationToken);
    }

    public async Task AddAsync(Brigade brigade, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(brigade);
        await context.Brigades.AddAsync(brigade, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveAsync(Brigade brigade, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(brigade);
        if (context.Entry(brigade).State == EntityState.Detached)
            context.Brigades.Update(brigade);
        await context.SaveChangesAsync(cancellationToken);
    }
}
