using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Shared.Common.Common
{
    /// <summary>
    /// Selects a configured EF Core provider and falls back to SQLite when the preferred
    /// relational database is unavailable.
    /// </summary>
    public sealed class DatabaseProviderFactory
    {
        private readonly IReadOnlyDictionary<string, IDatabaseProviderStrategy> _strategies;
        private readonly ILogger<DatabaseProviderFactory> _logger;

        public DatabaseProviderFactory(
            IEnumerable<IDatabaseProviderStrategy> strategies,
            ILogger<DatabaseProviderFactory> logger)
        {
            ArgumentNullException.ThrowIfNull(strategies);
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _strategies = strategies.ToDictionary(
                strategy => strategy.ProviderName,
                StringComparer.OrdinalIgnoreCase);

            if (!_strategies.ContainsKey("Sqlite"))
                throw new InvalidOperationException("A SQLite database provider strategy must be registered.");
        }

        public (IDatabaseProviderStrategy Strategy, string ConnectionString) Resolve(
            string preferredProvider,
            string primaryConnectionString,
            string sqliteFallbackConnectionString)
        {
            if (string.IsNullOrWhiteSpace(sqliteFallbackConnectionString))
                throw new ArgumentException("A SQLite fallback connection string is required.", nameof(sqliteFallbackConnectionString));

            if (string.Equals(preferredProvider, "Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Using SQLite provider as configured.");
                return (_strategies["Sqlite"], sqliteFallbackConnectionString);
            }

            if (!_strategies.TryGetValue(preferredProvider ?? string.Empty, out var preferred))
            {
                _logger.LogWarning("Database provider {Provider} is not registered; falling back to SQLite.",
                    string.IsNullOrWhiteSpace(preferredProvider) ? "(not configured)" : preferredProvider);
                return (_strategies["Sqlite"], sqliteFallbackConnectionString);
            }

            if (!string.IsNullOrWhiteSpace(primaryConnectionString))
            {
                try
                {
                    if (preferred.CanConnect(primaryConnectionString))
                    {
                        _logger.LogInformation("Database provider {Provider} is reachable.", preferred.ProviderName);
                        return (preferred, primaryConnectionString);
                    }
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(exception, "Database connectivity check for {Provider} failed.", preferred.ProviderName);
                }
            }

            _logger.LogWarning("Database provider {Provider} is unavailable; falling back to SQLite.", preferred.ProviderName);
            return (_strategies["Sqlite"], sqliteFallbackConnectionString);
        }

        public DbContextOptionsBuilder<TContext> ConfigureContext<TContext>(
            DbContextOptionsBuilder<TContext> builder,
            string preferredProvider,
            string primaryConnectionString,
            string sqliteFallbackConnectionString)
            where TContext : DbContext
        {
            ArgumentNullException.ThrowIfNull(builder);
            var (strategy, connectionString) = Resolve(
                preferredProvider, primaryConnectionString, sqliteFallbackConnectionString);
            return strategy.Configure(builder, connectionString);
        }
    }
}