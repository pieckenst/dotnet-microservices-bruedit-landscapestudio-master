using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

namespace Shared.Common.Common
{
    /// <summary>
    /// SQLite fallback database provider strategy — always available as a local file-based fallback.
    /// Ensures the system can run even when PostgreSQL/SQL Server is unreachable.
    /// </summary>
    public class SqliteProviderStrategy : IDatabaseProviderStrategy
    {
        public string ProviderName => "Sqlite";

        public DbContextOptionsBuilder Configure(DbContextOptionsBuilder builder, string connectionString)
        {
            return builder.UseSqlite(connectionString);
        }

        public bool CanConnect(string connectionString)
        {
            try
            {
                using var conn = new SqliteConnection(connectionString);
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
