using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Shared.Common.Common
{
    /// <summary>
    /// PostgreSQL database provider strategy — the primary relational DB option.
    /// </summary>
    public class PostgresProviderStrategy : IDatabaseProviderStrategy
    {
        public string ProviderName => "PostgreSQL";

        public DbContextOptionsBuilder<T> Configure<T>(DbContextOptionsBuilder<T> builder, string connectionString)
            where T : DbContext
        {
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
            return builder.UseNpgsql(connectionString);
        }

        public bool CanConnect(string connectionString)
        {
            try
            {
                using var conn = new NpgsqlConnection(connectionString);
                conn.Open();
                return conn.State == System.Data.ConnectionState.Open;
            }
            catch
            {
                return false;
            }
        }
    }
}
