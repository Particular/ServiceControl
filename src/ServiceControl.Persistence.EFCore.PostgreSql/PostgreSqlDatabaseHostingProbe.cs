namespace ServiceControl.Persistence.EFCore.PostgreSql;

using System.Data.Common;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using ServiceControl.Persistence;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Infrastructure;

class PostgreSqlDatabaseHostingProbe(PostgreSqlPersisterSettings settings, IServiceScopeFactory scopeFactory, ILogger<PostgreSqlDatabaseHostingProbe> logger) : IDatabaseHostingProbe
{
    public string StorageName => "PostgreSQL";

    public async Task<DatabaseHosting> Probe(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

            await using var command = dbContext.Database.GetDbConnection().CreateCommand();
            command.CommandText = ProbeSql;
            command.CommandTimeout = ProbeTimeoutSeconds;

            await dbContext.Database.OpenConnectionAsync(cancellationToken);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                return HostingFromConnectionString();
            }

            return new DatabaseHosting(HostingFromRoles(reader), MajorVersion(reader));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            logger.LogDebug(e, "Could not ask PostgreSQL how it is hosted, falling back to the connection string");

            return HostingFromConnectionString();
        }
    }

    // PostgreSQL has no equivalent of SQL Server's EngineEdition, but every managed offering creates
    // a distinctive administrative role that a self-hosted server does not have.
    string HostingFromRoles(DbDataReader reader)
    {
        if (reader.GetBoolean(1))
        {
            return "AzurePostgres";
        }

        if (reader.GetBoolean(2))
        {
            return "AwsRds";
        }

        return reader.GetBoolean(3) ? "GoogleCloudSql" : HostingFromConnectionString().Hosting;
    }

    static string MajorVersion(DbDataReader reader) =>
        reader.IsDBNull(0) ? DatabaseHostClassifier.Unknown : (reader.GetInt32(0) / 10000).ToString(CultureInfo.InvariantCulture);

    DatabaseHosting HostingFromConnectionString()
    {
        try
        {
            var host = new NpgsqlConnectionStringBuilder(settings.ConnectionString).Host;

            return new DatabaseHosting(DatabaseHostClassifier.Classify(host), DatabaseHostClassifier.Unknown);
        }
        catch (Exception e)
        {
            logger.LogDebug(e, "Could not classify the configured PostgreSQL host");

            return DatabaseHosting.Unavailable;
        }
    }

    const string ProbeSql = """
        SELECT current_setting('server_version_num')::int,
               EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'azure_pg_admin'),
               EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'rds_superuser'),
               EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'cloudsqlsuperuser')
        """;

    const int ProbeTimeoutSeconds = 5;
}
