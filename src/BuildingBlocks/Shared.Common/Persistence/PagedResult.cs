namespace Shared.Common.Persistence;

/// <summary>
/// A page of query results. TotalCount describes the full filtered result set,
/// not just the records returned in this page.
/// </summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => TotalCount == 0
        ? 0
        : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
