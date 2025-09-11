namespace ServiceControl.Audit.Persistence.PostgreSQL;

using Npgsql;
using System.Threading.Tasks;
using System.Threading;

class PostgreSQLConnectionFactory
{
    readonly NpgsqlDataSource dataSource;
    readonly NpgsqlDataSource dataSourceAdmin;

    public PostgreSQLConnectionFactory(DatabaseConfiguration databaseConfiguration)
    {
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(databaseConfiguration.ConnectionString)
        {
            Name = "ServiceControl.Audit"
        };
        //dataSourceBuilder.UseLoggerFactory(loggerFactory);
        dataSourceBuilder.EnableDynamicJson();
        dataSource = dataSourceBuilder.Build();

        var builder = new NpgsqlConnectionStringBuilder(databaseConfiguration.ConnectionString)
        {
            Database = databaseConfiguration.AdminDatabaseName
        };
        var dataSourceBuilderAdmin = new NpgsqlDataSourceBuilder(builder.ConnectionString)
        {
            Name = "ServiceControl.Audit-admin",
        };
        //dataSourceBuilderAdmin.UseLoggerFactory(loggerFactory);
        dataSourceBuilderAdmin.EnableDynamicJson();
        dataSourceAdmin = dataSourceBuilderAdmin.Build();
    }

    public async Task<NpgsqlConnection> OpenConnection(CancellationToken cancellationToken)
    {
        var conn = await dataSource.OpenConnectionAsync(cancellationToken);
        return conn;
    }

    public async Task<NpgsqlConnection> OpenAdminConnection(CancellationToken cancellationToken)
    {
        var conn = dataSourceAdmin.CreateConnection();
        await conn.OpenAsync(cancellationToken);
        return conn;
    }
}