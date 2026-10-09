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
            // ProductVersion rather than ProductMajorVersion: the latter is documented as SQL Server
            // only and comes back null on Azure SQL Database, Managed Instance and Synapse.
            command.CommandText = "SELECT SERVERPROPERTY('EngineEdition'), SERVERPROPERTY('ProductVersion'), DB_ID('rdsadmin'), CAST(DATABASEPROPERTYEX(DB_NAME(), 'ServiceObjective') AS nvarchar(64))";
            command.CommandTimeout = ProbeTimeoutSeconds;

            await dbContext.Database.OpenConnectionAsync(cancellationToken);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken) || reader.IsDBNull(0))
            {
                return HostingFromConnectionString();
            }

            var engineEdition = Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture);

            var rds = !reader.IsDBNull(2);

            return new DatabaseHosting(
                HostingFor(engineEdition, rds, ConfiguredHost),
                MajorVersion(reader),
                DatabaseHostingSource.Probe,
                EditionFamily(engineEdition),
                ServiceObjectiveTier(reader.IsDBNull(3) ? null : reader.GetString(3)));
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

    /// <summary>
    /// Maps an EngineEdition to a hosting classification. The Azure editions identify their service
    /// outright. The ordinary editions say only that this is a regular SQL Server, which still
    /// leaves RDS and Cloud SQL in play, so the host name decides before self-hosted is concluded.
    /// Synapse and Fabric are not mapped: ServiceControl does not run on them, and an edition we do
    /// not recognise is not evidence of an ordinary SQL Server, so it falls to the host name.
    /// </summary>
    internal static string HostingFor(int engineEdition, bool rds, string? host) => engineEdition switch
    {
        AzureSqlDatabase => "AzureSql",
        AzureSqlManagedInstance => "AzureSqlManagedInstance",
        AzureSqlEdge => "AzureSqlEdge",
        PersonalOrDesktop or Standard or Enterprise or Express => rds ? "AwsRds" : ManagedOrSelfHosted(host),
        _ => DatabaseHostClassifier.Classify(host)
    };

    /// <summary>
    /// Developer and Evaluation run the Enterprise engine and report Enterprise, so the report never
    /// says which licence an instance runs under. The managed Azure services have no edition.
    /// </summary>
    internal static string EditionFamily(int engineEdition) => engineEdition switch
    {
        Standard => "Standard",
        Enterprise => "Enterprise",
        Express => "Express",
        AzureSqlDatabase or AzureSqlManagedInstance => DatabaseHosting.NotApplicable,
        _ => "Other"
    };

    /// <summary>
    /// Keeps only the tier. The full service objective also encodes the hardware series, and a
    /// DC-series name suggests Always Encrypted with secure enclaves.
    /// </summary>
    internal static string ServiceObjectiveTier(string? serviceObjective)
    {
        if (string.IsNullOrWhiteSpace(serviceObjective))
        {
            return DatabaseHosting.NotApplicable;
        }

        var objective = serviceObjective.Trim().ToUpperInvariant();

        return objective switch
        {
            "BASIC" => "Basic",
            "ELASTICPOOL" => "ElasticPool",
            _ when objective.StartsWith("GP_", StringComparison.Ordinal) => "GeneralPurpose",
            _ when objective.StartsWith("BC_", StringComparison.Ordinal) => "BusinessCritical",
            _ when objective.StartsWith("HS_", StringComparison.Ordinal) => "Hyperscale",
            _ when IsDtuObjective(objective, 'S') => "Standard",
            _ when IsDtuObjective(objective, 'P') => "Premium",
            _ => "Other"
        };
    }

    static bool IsDtuObjective(string objective, char tier) =>
        objective.Length > 1 && objective[0] == tier && objective.AsSpan(1).IndexOfAnyExceptInRange('0', '9') < 0;

    static string ManagedOrSelfHosted(string? host)
    {
        var classified = DatabaseHostClassifier.Classify(host);

        return classified == DatabaseHostClassifier.Unknown ? DatabaseHostClassifier.SelfHosted : classified;
    }

    static string MajorVersion(DbDataReader reader)
    {
        if (reader.IsDBNull(1))
        {
            return DatabaseHostClassifier.Unknown;
        }

        var productVersion = Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture);

        if (string.IsNullOrWhiteSpace(productVersion))
        {
            return DatabaseHostClassifier.Unknown;
        }

        var major = productVersion.Split('.')[0];

        return string.IsNullOrWhiteSpace(major) ? DatabaseHostClassifier.Unknown : major;
    }

    DatabaseHosting HostingFromConnectionString()
    {
        var host = ConfiguredHost;

        return host is null
            ? DatabaseHosting.Unclassified with { ServerEdition = DatabaseHostClassifier.Unknown, ServiceObjective = DatabaseHostClassifier.Unknown }
            : new DatabaseHosting(DatabaseHostClassifier.Classify(host), DatabaseHostClassifier.Unknown, DatabaseHostingSource.ConnectionString, DatabaseHostClassifier.Unknown, DatabaseHostClassifier.Unknown);
    }

    string? ConfiguredHost
    {
        get
        {
            try
            {
                return new SqlConnectionStringBuilder(settings.ConnectionString).DataSource;
            }
            catch (Exception e)
            {
                logger.LogDebug(e, "Could not read the configured SQL Server host");

                return null;
            }
        }
    }

    const int ProbeTimeoutSeconds = 5;

    // https://learn.microsoft.com/en-us/sql/t-sql/functions/serverproperty-transact-sql
    const int PersonalOrDesktop = 1;
    const int Standard = 2;
    const int Enterprise = 3;
    const int Express = 4;
    const int AzureSqlDatabase = 5;
    const int AzureSqlManagedInstance = 8;
    const int AzureSqlEdge = 9;
}
