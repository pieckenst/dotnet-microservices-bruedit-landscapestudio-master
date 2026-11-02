namespace Procurement.Application.Contracts;

/// <summary>
/// Executes a group of repository operations in one database transaction. The repositories used
/// within the callback share the same scoped EF Core context.
/// </summary>
public interface IProcurementUnitOfWork
{
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);
}