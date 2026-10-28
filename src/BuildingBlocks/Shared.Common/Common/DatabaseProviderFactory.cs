using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Shared.Common.Common
{
    /// <summary>
    /// Factory pattern + Strategy pattern: resolves the appropriate database provider at startup.
    /// Tries the preferred provider first; if it cannot connect, automatically falls back to SQLite.
    /// This guarantees the system is always operational — "fallback to local sqlite always possible".
    /// </summary>
    public class DatabaseProviderFactory
    {
        private readonly IEnumerable<IDatabaseProviderStrategy> _strategies;
        private readonly ILogger<DatabaseProviderFactory> _logger;

        public DatabaseProviderFactory(IEnumerable<IDatabaseProviderStrategy> strategies, ILogger<DatabaseProviderFactory> logger)
        {
            _strategies = strategies ?? throw new ArgumentNullException(nameof(strategies));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Resolves the best available provider strategy based on configuration preference and connectivity.
        /// </summary>
        /// <param name="preferredProvider">The provider name to try first ("PostgreSQL", "SqlServer", "Sqlite").</param>
        /// <param name="primaryConnectionString">Connection string for the preferred provider.</param>
        /// <param name="sqliteFallbackConnectionString">SQLite connection string used if primary is unreachable.</param>
        /// <returns>The resolved strategy and its connection string.</returns>
        public (IDatabaseProviderStrategy Strategy, string ConnectionString) Resolve(
            string preferredProvider,
            string primaryConnectionString,
            string sqliteFallbackConnectionString)
        {
            // If the preferred is SQLite, just use it directly
            if (string.Equals(preferredProvider, "Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                var sqliteStrategy = GetStrategy("Sqlite");
                _logger.LogInformation("Using SQLite provider directly: {ConnectionString}", sqliteFallbackConnectionString);
                return (sqliteStrategy, sqliteFallbackConnectionString);
            }

            // Try the preferred provider
            var preferred = GetStrategy(preferredProvider);
            if (preferred != null && !string.IsNullOrEmpty(primaryConnectionString))
            {
                _logger.LogInformation("Attempting to connect to {Provider} at: {ConnectionString}", preferred.ProviderName, primaryConnectionString);
                if (preferred.CanConnect(primaryConnectionString))
                {
                    _logger.LogInformation("{Provider} connection successful.", preferred.ProviderName);
                    return (preferred, primaryConnectionString);
                }
                _logger.LogWarning("{Provider} is unreachable. Falling back to SQLite.", preferred.ProviderName);
            }

            // Fallback to SQLite
            var fallback = GetStrategy("Sqlite") ?? throw new InvalidOperationException("SQLite strategy not registered.");
            _logger.LogWarning("Falling back to SQLite: {ConnectionString}", sqliteFallbackConnectionString);
            return (fallback, sqliteFallbackConnectionString);
        }

        /// <summary>
        /// Configures the DbContextOptionsBuilder with the resolved provider.
        /// </summary>
        public DbContextOptionsBuilder<TContext> ConfigureContext<TContext>(
            DbContextOptionsBuilder<TContext> builder,
            string preferredProvider,
            string primaryConnectionString,
            string sqliteFallbackConnectionString)
            where TContext : DbContext
        {
            var (strategy, connectionString) = Resolve(preferredProvider, primaryConnectionString, sqliteFallbackConnectionString);
            return strategy.Configure(builder, connectionString);
        }

        private IDatabaseProviderStrategy GetStrategy(string providerName)
        {
            return _strategies.FirstOrDefault(s =>
                string.Equals(s.ProviderName, providerName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
