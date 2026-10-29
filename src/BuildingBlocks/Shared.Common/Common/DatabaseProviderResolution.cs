namespace Shared.Common.Common;

/// <summary>
/// The selected relational provider for a running service instance. Resolve this once at startup
/// so a transient database outage cannot silently switch requests onto a separate SQLite store.
/// </summary>
public sealed record DatabaseProviderResolution(
    IDatabaseProviderStrategy Strategy,
    string ConnectionString);
