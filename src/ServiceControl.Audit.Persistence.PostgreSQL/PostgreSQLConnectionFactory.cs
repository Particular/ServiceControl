namespace ServiceControl.Audit.Persistence.PostgreSQL
{
    using Npgsql;
    using System.Threading.Tasks;
    using System.Threading;

    class PostgreSQLConnectionFactory
    {
        readonly string connectionString;

        public PostgreSQLConnectionFactory(DatabaseConfiguration databaseConfiguration) => connectionString = databaseConfiguration.ConnectionString;

        public async Task<NpgsqlConnection> OpenConnection(CancellationToken cancellationToken)
        {
            var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync(cancellationToken);
            return conn;
        }
    }
}
