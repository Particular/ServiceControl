namespace ServiceControl.Audit.Persistence.PostgreSQL
{
    using Npgsql;
    using System.Threading.Tasks;
    using System.Threading;

    public class PostgreSQLConnectionFactory
    {
        readonly string connectionString;

        public PostgreSQLConnectionFactory(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public NpgsqlConnection CreateConnection()
        {
            return new NpgsqlConnection(connectionString);
        }

        public async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
        {
            var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
            return conn;
        }
    }
}
