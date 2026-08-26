namespace ServiceControl.Persistence.EFCore.SqlServer;

using System.Data.Common;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ServiceControl.Persistence;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Infrastructure;

class SqlServerDatabaseHostingProbe(SqlServerPersisterSettings settings, IServiceScopeFactory scopeFactory, ILogger<SqlServerDatabaseHostingProbe> logger) : IDatabaseHostingProbe
{
    public string StorageName => "SQLServer";

    public async Task<DatabaseHosting> Probe(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

            await using var command = dbContext.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT SERVERPROPERTY('EngineEdition'), SERVERPROPERTY('ProductMajorVersion')";
            command.CommandTimeout = ProbeTimeoutSeconds;

            await dbContext.Database.OpenConnectionAsync(cancellationToken);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                return HostingFromConnectionString();
            }

            return new DatabaseHosting(HostingFromEngineEdition(reader), MajorVersion(reader));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            logger.LogDebug(e, "Could not ask SQL Server how it is hosted, falling back to the connection string");

            return HostingFromConnectionString();
        }
    }

    // EngineEdition is the authoritative answer: the server itself reports which Azure service it is,
    // where a host name suffix is only a guess. Anything on-premises falls back to the suffix.
    string HostingFromEngineEdition(DbDataReader reader) =>
        reader.IsDBNull(0) ? HostingFromConnectionString().Hosting : Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture) switch
        {
            AzureSqlDatabaseEdition => "AzureSql",
            AzureSqlManagedInstanceEdition => "AzureSqlManagedInstance",
            AzureSynapseEdition => "AzureSynapse",
            _ => HostingFromConnectionString().Hosting
        };

    static string MajorVersion(DbDataReader reader) =>
        reader.IsDBNull(1) ? DatabaseHostClassifier.Unknown : Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? DatabaseHostClassifier.Unknown;

    DatabaseHosting HostingFromConnectionString()
    {
        try
        {
            var host = new SqlConnectionStringBuilder(settings.ConnectionString).DataSource;

            return new DatabaseHosting(DatabaseHostClassifier.Classify(host), DatabaseHostClassifier.Unknown);
        }
        catch (Exception e)
        {
            logger.LogDebug(e, "Could not classify the configured SQL Server host");

            return DatabaseHosting.Unavailable;
        }
    }

    const int ProbeTimeoutSeconds = 5;
    const int AzureSqlDatabaseEdition = 5;
    const int AzureSqlManagedInstanceEdition = 8;
    const int AzureSynapseEdition = 11;
}
