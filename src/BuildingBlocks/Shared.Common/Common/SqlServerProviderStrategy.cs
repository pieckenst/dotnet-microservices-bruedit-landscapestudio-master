using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

namespace Shared.Common.Common
{
    /// <summary>
    /// Microsoft SQL Server database provider strategy — alternative relational DB option.
    /// </summary>
    public class SqlServerProviderStrategy : IDatabaseProviderStrategy
    {
        public string ProviderName => "SqlServer";

        public DbContextOptionsBuilder Configure(DbContextOptionsBuilder builder, string connectionString)
        {
            return builder.UseSqlServer(connectionString);
        }

        public bool CanConnect(string connectionString)
        {
            try
            {
                using var conn = new SqlConnection(connectionString);
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
