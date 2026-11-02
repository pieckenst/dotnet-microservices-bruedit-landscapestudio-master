namespace Organization.Application.Contracts;

/// <summary>
/// Validates remote Project-owned references without creating cross-service database foreign keys.
/// </summary>
public interface IProjectAssignmentLookup
{
    Task<string?> GetProjectStatusAsync(Guid projectId, CancellationToken cancellationToken);
    Task<string?> GetProjectTaskStatusAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken);
}
