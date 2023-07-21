namespace ServiceControl.Persistence.SqlServer
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using ServiceControl.Persistence.Infrastructure;

    class SqlDbConnectionManager
    {
        public string ConnectionString { get; }

        public SqlDbConnectionManager(string connectionString) => ConnectionString = connectionString;
    }

    static class SqlDbConnectionManagerExtensions
    {
        public static async Task Perform(this SqlDbConnectionManager manager, Func<IDbConnection, Task> action)
        {
            using (var connection = new SqlConnection(manager.ConnectionString))
            {
                await connection.OpenAsync();
                await action(connection);
            }
        }

        public static async Task<QueryResult<T>> PagedQuery<T>(this SqlDbConnectionManager manager, Func<IDbConnection, Task<QueryResult<T>>> action) where T : class
        {
            using (var connection = new SqlConnection(manager.ConnectionString))
            {
                await connection.OpenAsync();

                var result = await action(connection);

                return result;
            }
        }
    }
}