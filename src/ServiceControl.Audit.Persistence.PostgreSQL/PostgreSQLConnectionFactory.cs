namespace ServiceControl.Audit.Persistence.PostgreSQL;

using Npgsql;
using System.Threading.Tasks;
using System.Threading;
class PostgreSQLConnectionFactory(DatabaseConfiguration databaseConfiguration)
{
    public async Task<NpgsqlConnection> OpenConnection(CancellationToken cancellationToken)
    {
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(databaseConfiguration.ConnectionString);
        dataSourceBuilder.EnableDynamicJson();
        var dataSource = dataSourceBuilder.Build();
        var conn = dataSource.CreateConnection();
        await conn.OpenAsync(cancellationToken);
        return conn;
    }

    public async Task<NpgsqlConnection> OpenAdminConnection(CancellationToken cancellationToken)
    {
        var builder = new NpgsqlConnectionStringBuilder(databaseConfiguration.ConnectionString)
        {
            Database = databaseConfiguration.AdminDatabaseName
        };
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(builder.ConnectionString);
        dataSourceBuilder.EnableDynamicJson();
        var dataSource = dataSourceBuilder.Build();
        var conn = dataSource.CreateConnection();
        await conn.OpenAsync(cancellationToken);
        return conn;
    }
}