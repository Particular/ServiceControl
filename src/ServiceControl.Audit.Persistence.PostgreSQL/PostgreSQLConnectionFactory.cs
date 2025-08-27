namespace ServiceControl.Audit.Persistence.PostgreSQL;

using Npgsql;
using System.Threading.Tasks;
using System.Threading;
class PostgreSQLConnectionFactory(DatabaseConfiguration databaseConfiguration)
{
    public async Task<NpgsqlConnection> OpenConnection(CancellationToken cancellationToken)
    {
        var conn = new NpgsqlConnection(databaseConfiguration.ConnectionString);
        await conn.OpenAsync(cancellationToken);
        return conn;
    }

    public async Task<NpgsqlConnection> OpenAdminConnection(CancellationToken cancellationToken)
    {
        var builder = new NpgsqlConnectionStringBuilder(databaseConfiguration.ConnectionString)
        {
            Database = databaseConfiguration.AdminDatabaseName
        };
        var conn = new NpgsqlConnection(builder.ConnectionString);
        await conn.OpenAsync(cancellationToken);
        return conn;
    }
}