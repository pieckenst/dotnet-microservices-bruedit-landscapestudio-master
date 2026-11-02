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

        var query = context.Estimates.AsNoTracking()
            .Include(estimate => estimate.LineItems)
            .Where(estimate => estimate.ProjectId == projectId);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(estimate => estimate.CreatedDate)
            .ThenBy(estimate => estimate.Version)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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

        // Prefer tracked aggregates. Update() on a detached graph marks every child as
        // Modified, which can produce concurrency exceptions for children not in storage.
        if (context.Entry(estimate).State == EntityState.Detached)
        {
            var tracked = await context.Estimates
                .Include(existing => existing.LineItems)
                .SingleOrDefaultAsync(existing => existing.Id == estimate.Id, cancellationToken);

            if (tracked is null)
                throw new KeyNotFoundException($"Estimate '{estimate.Id}' was not found.");

            context.Entry(tracked).CurrentValues.SetValues(estimate);
            foreach (var incomingItem in estimate.LineItems)
            {
                var existingItem = tracked.LineItems.SingleOrDefault(item => item.Id == incomingItem.Id);
                if (existingItem is null)
                {
                    tracked.AddLineItem(incomingItem);
                    context.EstimateLineItems.Add(incomingItem);
                }
                else
                {
                    context.Entry(existingItem).CurrentValues.SetValues(incomingItem);
                }
            }

            var incomingIds = estimate.LineItems.Select(item => item.Id).ToHashSet();
            foreach (var existingItem in tracked.LineItems.Where(item => !incomingIds.Contains(item.Id)).ToList())
            {
                tracked.RemoveLineItem(existingItem.Id);
                context.EstimateLineItems.Remove(existingItem);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddLineItemAsync(Estimate estimate, EstimateLineItem item, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(estimate);
        ArgumentNullException.ThrowIfNull(item);
        if (context.Entry(estimate).State == EntityState.Detached)
            throw new InvalidOperationException("Estimate must be loaded as a tracked aggregate before editing its line items.");
        if (item.EstimateId != estimate.Id)
            throw new ArgumentException("Line item belongs to a different estimate.", nameof(item));

        estimate.AddLineItem(item);
        // Explicitly mark the new child as Added. This avoids relying solely on navigation
        // collection change detection for a private-set aggregate collection.
        context.EstimateLineItems.Add(item);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveLineItemAsync(Estimate estimate, Guid lineItemId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(estimate);
        if (context.Entry(estimate).State == EntityState.Detached)
            throw new InvalidOperationException("Estimate must be loaded as a tracked aggregate before editing its line items.");

        var item = estimate.LineItems.SingleOrDefault(line => line.Id == lineItemId)
            ?? throw new KeyNotFoundException("Estimate line item was not found.");
        estimate.RemoveLineItem(lineItemId);
        context.EstimateLineItems.Remove(item);
        await context.SaveChangesAsync(cancellationToken);
    }
}
