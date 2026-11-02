using Project.Domain.Entities;
using Shared.Common.Persistence;

namespace Project.Application.Contracts;

/// <summary>Repository boundary for the Estimate aggregate, including its child line items.</summary>
public interface IEstimateRepository
{
    Task<Estimate?> GetByIdWithItemsAsync(Guid id, bool tracking, CancellationToken cancellationToken);
    Task<PagedResult<Estimate>> GetByProjectAsync(Guid projectId, int page, int pageSize, CancellationToken cancellationToken);
    Task AddAsync(Estimate estimate, CancellationToken cancellationToken);
    Task SaveAsync(Estimate estimate, CancellationToken cancellationToken);
    Task AddLineItemAsync(Estimate estimate, EstimateLineItem item, CancellationToken cancellationToken);
    Task RemoveLineItemAsync(Estimate estimate, Guid lineItemId, CancellationToken cancellationToken);
}
