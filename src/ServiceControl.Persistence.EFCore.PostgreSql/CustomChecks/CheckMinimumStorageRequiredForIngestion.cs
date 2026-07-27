namespace ServiceControl.Persistence.EFCore.PostgreSql.CustomChecks;

using System;
using System.Threading;
using System.Threading.Tasks;
using DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NServiceBus.CustomChecks;
using ServiceControl.Infrastructure;
using ServiceControl.Persistence;

class CheckMinimumStorageRequiredForIngestion(IServiceScopeFactory serviceScopeFactory, MinimumRequiredStorageState stateHolder, PostgreSqlPersisterSettings settings, ILogger<CheckMinimumStorageRequiredForIngestion> logger)
    : CustomCheck("Message Ingestion Process", "ServiceControl Health", TimeSpan.FromSeconds(5))
{
    public override async Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
    {
        var percentageThreshold = settings.MinimumStorageLeftRequiredForIngestion / 100m;

        logger.LogDebug("Check ServiceControl data drive space starting. Threshold {PercentageThreshold:P0}", percentageThreshold);

        var databaseSpace = await TryGetDatabaseSpace(cancellationToken);
        if (databaseSpace == null)
        {
            logger.LogWarning("Unable to retrieve database space information.");
            stateHolder.CanIngestMore = true;
            return CheckResult.Pass;
        }

        var (totalSpaceMb, availableFreeSpaceMb) = databaseSpace.Value;
        var percentRemaining = totalSpaceMb == 0 ? 0 : (decimal)availableFreeSpaceMb / totalSpaceMb;

        logger.LogDebug("Free space: {FreeSpaceTotalMbFree:N0}MB | Total: {FreeSpaceTotalMbAvailable:N0}MB | Remaining {PercentRemaining:P1}%", availableFreeSpaceMb, totalSpaceMb, percentRemaining);

        if (percentRemaining > percentageThreshold)
        {
            stateHolder.CanIngestMore = true;
            return CheckResult.Pass;
        }

        logger.LogWarning("Error message ingestion stopped! {PercentRemaining:P0} disk space remaining on PostgreSQL database. This is less than {PercentageThreshold}% - the minimal required space configured. The threshold can be set using the {ConfigKey} configuration setting.",
            percentRemaining,
            percentageThreshold,
            nameof(PostgreSqlPersisterSettings.MinimumStorageLeftRequiredForIngestion));
        stateHolder.CanIngestMore = false;
        return CheckResult.Failed($"Error message ingestion stopped! {percentRemaining:P0} disk space remaining on PostgreSQL database. This is less than {percentageThreshold}% - the minimal required space configured. The threshold can be set using the {nameof(PostgreSqlPersisterSettings.MinimumStorageLeftRequiredForIngestion)} configuration setting.");
    }

    public static void Validate(PostgreSqlPersisterSettings settings)
    {
        var logger = LoggerUtil.CreateStaticLogger<CheckMinimumStorageRequiredForIngestion>();
        var threshold = settings.MinimumStorageLeftRequiredForIngestion;
        var settingName = nameof(PostgreSqlPersisterSettings.MinimumStorageLeftRequiredForIngestion);

        if (threshold < 0)
        {
            logger.LogCritical("{SettingName} is invalid, minimum value is 0", settingName);
            throw new Exception($"{settingName} is invalid, minimum value is 0.");
        }

        if (threshold > 100)
        {
            logger.LogCritical("{SettingName} is invalid, maximum value is 100", settingName);
            throw new Exception($"{settingName} is invalid, maximum value is 100.");
        }
    }

    internal async Task<(long totalSpaceMb, long availableFreeSpaceMb)?> TryGetDatabaseSpace(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();
            return await CheckFreeDiskSpace.TryGetDatabaseSpace(dbContext, cancellationToken);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
