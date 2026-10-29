using Microsoft.EntityFrameworkCore;
using Project.Application.Contracts;
using Project.Domain.Entities;
using Project.Infrastructure.Persistence;
using Shared.Common.Persistence;

namespace Project.Infrastructure.Repositories;

public sealed class EstimateRepository(ProjectContext context) : IEstimateRepository
{
    public Task<Estimate?> GetByIdWithItemsAsync(Guid id, bool tracking, CancellationToken cancellationToken)
    {
        IQueryable<Estimate> query = context.Estimates.Include(estimate => estimate.LineItems);
        if (!tracking) query = query.AsNoTracking();
        return query.SingleOrDefaultAsync(estimate => estimate.Id == id, cancellationToken);
    }

    public async Task<PagedResult<Estimate>> GetByProjectAsync(
        Guid projectId, int page, int pageSize, CancellationToken cancellationToken)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize is < 1 or > 200) throw new ArgumentOutOfRangeException(nameof(pageSize));
        var query = context.Estimates.AsNoTracking().Include(estimate => estimate.LineItems)
            .Where(estimate => estimate.ProjectId == projectId);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(estimate => estimate.CreatedDate)
            .ThenBy(estimate => estimate.Version)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<Estimate>(items, page, pageSize, totalCount);
    }

    public async Task AddAsync(Estimate estimate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(estimate);
        await context.Estimates.AddAsync(estimate, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveAsync(Estimate estimate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(estimate);
        if (context.Entry(estimate).State == EntityState.Detached)
            context.Estimates.Update(estimate);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddLineItemAsync(Estimate estimate, EstimateLineItem item, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(estimate);
        ArgumentNullException.ThrowIfNull(item);
        if (context.Entry(estimate).State == EntityState.Detached)
            throw new InvalidOperationException("Estimate must be loaded as a tracked aggregate before editing its line items.");
        estimate.AddLineItem(item);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveLineItemAsync(Estimate estimate, Guid lineItemId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(estimate);
        if (context.Entry(estimate).State == EntityState.Detached)
            throw new InvalidOperationException("Estimate must be loaded as a tracked aggregate before editing its line items.");
        estimate.RemoveLineItem(lineItemId);
        await context.SaveChangesAsync(cancellationToken);
    }
}
