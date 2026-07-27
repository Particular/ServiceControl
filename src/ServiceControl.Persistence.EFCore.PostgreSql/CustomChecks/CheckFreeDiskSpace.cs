namespace ServiceControl.Persistence.EFCore.PostgreSql.CustomChecks;

using System;
using System.Data;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NServiceBus.CustomChecks;
using ServiceControl.Infrastructure;
using ServiceControl.Persistence;

class CheckFreeDiskSpace(IServiceScopeFactory serviceScopeFactory, PostgreSqlPersisterSettings settings, ILogger<CheckFreeDiskSpace> logger)
    : CustomCheck("ServiceControl database", "Storage space", TimeSpan.FromMinutes(5))
{
    public override async Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Check ServiceControl data drive space remaining custom check starting. Threshold {PercentageThreshold:P0}", percentageThreshold);

        var databaseSpace = await TryGetDatabaseSpace(cancellationToken);
        if (databaseSpace == null)
        {
            logger.LogWarning("Unable to retrieve database space information.");
            return CheckResult.Pass;
        }

        var (totalSpaceMb, availableFreeSpaceMb) = databaseSpace.Value;
        var percentRemaining = totalSpaceMb == 0 ? 0 : (decimal)availableFreeSpaceMb / totalSpaceMb;

        logger.LogDebug("Free space: {FreeSpaceTotalMbFree:N0}MB | Total: {FreeSpaceTotalMbAvailable:N0}MB | Remaining {PercentRemaining:P1}%", availableFreeSpaceMb, totalSpaceMb, percentRemaining);

        return percentRemaining > percentageThreshold
            ? CheckResult.Pass
            : CheckResult.Failed($"{percentRemaining:P0} disk space remaining on PostgreSQL database.");
    }

    public static void Validate(PostgreSqlPersisterSettings settings)
    {
        var logger = LoggerUtil.CreateStaticLogger<CheckFreeDiskSpace>();
        var threshold = settings.DataSpaceRemainingThreshold;

        if (threshold < 0)
        {
            logger.LogCritical("{ConfigKey} is invalid, minimum value is 0", PersistenceConfiguration.DataSpaceRemainingThresholdKey);
            throw new Exception($"{PersistenceConfiguration.DataSpaceRemainingThresholdKey} is invalid, minimum value is 0.");
        }

        if (threshold > 100)
        {
            logger.LogCritical("{ConfigKey} is invalid, maximum value is 100", PersistenceConfiguration.DataSpaceRemainingThresholdKey);
            throw new Exception($"{PersistenceConfiguration.DataSpaceRemainingThresholdKey} is invalid, maximum value is 100.");
        }
    }

    internal async Task<(long totalSpaceMb, long availableFreeSpaceMb)?> TryGetDatabaseSpace(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();
            return await TryGetDatabaseSpace(dbContext, cancellationToken);
        }
        catch (Exception)
        {
            return null;
        }
    }

    internal static async Task<(long totalSpaceMb, long availableFreeSpaceMb)?> TryGetDatabaseSpace(DbContext dbContext, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = dbContext.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await using var command = connection.CreateCommand();
            command.CommandText = """
                                  SELECT current_setting('data_directory');
                                  """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken) || reader.IsDBNull(0))
            {
                return null;
            }

            var dataDirectory = reader.GetString(0);
            var dataPathRoot = Path.GetPathRoot(dataDirectory);
            if (string.IsNullOrWhiteSpace(dataPathRoot))
            {
                return null;
            }

            var dataDriveInfo = new DriveInfo(dataPathRoot);
            var totalSpaceMb = dataDriveInfo.TotalSize / 1024 / 1024;
            var availableFreeSpaceMb = dataDriveInfo.AvailableFreeSpace / 1024 / 1024;

            return (totalSpaceMb, availableFreeSpaceMb);
        }
        catch (Exception)
        {
            return null;
        }
    }

    readonly decimal percentageThreshold = settings.DataSpaceRemainingThreshold / 100m;
}
