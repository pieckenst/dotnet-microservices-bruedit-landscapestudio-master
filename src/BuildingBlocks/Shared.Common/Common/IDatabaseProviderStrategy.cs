using Microsoft.EntityFrameworkCore;

namespace Shared.Common.Common
{
    /// <summary>
    /// Strategy pattern interface for selecting the EF Core database provider at runtime.
    /// Each implementation configures a specific provider (PostgreSQL, SQL Server, SQLite).
    /// </summary>
    public interface IDatabaseProviderStrategy
    {
        /// <summary>Gets the unique name of the provider (e.g. "PostgreSQL", "SqlServer", "Sqlite").</summary>
        string ProviderName { get; }

        /// <summary>Configures the DbContextOptionsBuilder with the appropriate EF Core provider.</summary>
        DbContextOptionsBuilder Configure(DbContextOptionsBuilder builder, string connectionString);

        /// <summary>Attempts to verify connectivity to the database with the given connection string.</summary>
        bool CanConnect(string connectionString);
    }
}
