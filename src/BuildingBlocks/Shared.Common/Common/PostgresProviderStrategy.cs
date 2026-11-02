using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Shared.Common.Common
{
    /// <summary>
    /// PostgreSQL database provider strategy — the primary relational DB option.
    /// Npgsql's modern DateTime mapping is UTC DateTime -> timestamp with time zone.
    /// Do not enable legacy timestamp behavior here: it maps DateTime to
    /// timestamp without time zone and conflicts with the UTC audit timestamps
    /// written by the services.
    /// </summary>
    public sealed class PostgresProviderStrategy : IDatabaseProviderStrategy
    {
        public string ProviderName => "PostgreSQL";

        public DbContextOptionsBuilder Configure(DbContextOptionsBuilder builder, string connectionString)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
            return builder.UseNpgsql(connectionString);
        }

        public bool CanConnect(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString)) return false;

            try
            {
                using var connection = new NpgsqlConnection(connectionString);
                connection.Open();
                return connection.State == System.Data.ConnectionState.Open;
            }
            catch (NpgsqlException)
            {
                return false;
            }
            catch (TimeoutException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }
}
