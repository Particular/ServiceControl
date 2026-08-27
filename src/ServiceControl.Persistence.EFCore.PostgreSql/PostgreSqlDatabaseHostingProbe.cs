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

            var hosting = HostingFor(reader.GetBoolean(1), reader.GetBoolean(2), reader.GetBoolean(3), ConfiguredHost);

            return new DatabaseHosting(hosting, MajorVersion(reader), DatabaseHostingSource.Probe);
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

    /// <summary>
    /// PostgreSQL has no equivalent of SQL Server's EngineEdition, but every managed offering
    /// creates a distinctive administrative role that a self-hosted server does not have. A server
    /// that answered and has none of them is self-hosted, unless its host name names a managed
    /// service that does not announce itself this way.
    /// </summary>
    internal static string HostingFor(bool azure, bool rds, bool cloudSql, string? host)
    {
        if (azure)
        {
            return "AzurePostgres";
        }

        if (rds)
        {
            return "AwsRds";
        }

        if (cloudSql)
        {
            return "GoogleCloudSql";
        }

        var classified = DatabaseHostClassifier.Classify(host);

        return classified == DatabaseHostClassifier.Unknown ? DatabaseHostClassifier.SelfHosted : classified;
    }

    static string MajorVersion(DbDataReader reader) =>
        reader.IsDBNull(0) ? DatabaseHostClassifier.Unknown : (reader.GetInt32(0) / 10000).ToString(CultureInfo.InvariantCulture);

    DatabaseHosting HostingFromConnectionString()
    {
        var host = ConfiguredHost;

        return host is null
            ? DatabaseHosting.Unclassified
            : new DatabaseHosting(DatabaseHostClassifier.Classify(host), DatabaseHostClassifier.Unknown, DatabaseHostingSource.ConnectionString);
    }

    string? ConfiguredHost
    {
        get
        {
            try
            {
                return new NpgsqlConnectionStringBuilder(settings.ConnectionString).Host;
            }
            catch (Exception e)
            {
                logger.LogDebug(e, "Could not read the configured PostgreSQL host");

                return null;
            }
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
