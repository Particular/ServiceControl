namespace ServiceControl.Audit.Persistence.RavenDB.CustomChecks
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using NServiceBus.CustomChecks;
    using RavenDB;
    using ServiceControl.Infrastructure;

    class CheckMinimumStorageRequiredForIngestion(MinimumRequiredStorageState stateHolder, DatabaseConfiguration databaseConfiguration, ILogger<CheckMinimumStorageRequiredForIngestion> logger) : CustomCheck("Audit Message Ingestion Process", "ServiceControl.Audit Health", TimeSpan.FromSeconds(5))
    {
        public override Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
        {
            var percentageThreshold = databaseConfiguration.MinimumStorageLeftRequiredForIngestion / 100m;
            logger.LogDebug("Check ServiceControl data drive space starting. Threshold {PercentageThreshold:P0}", percentageThreshold);

            // Should be checking UseEmbeddedServer but need to check DbPath instead for the ATT hack to work
            if (string.IsNullOrEmpty(databaseConfiguration.ServerConfiguration.DbPath))
            {
                stateHolder.CanIngestMore = true;
                return SuccessResult;
            }

            var dataPathRoot = Path.GetPathRoot(databaseConfiguration.ServerConfiguration.DbPath) ?? throw new Exception($"Unable to find the root of the data path {databaseConfiguration.ServerConfiguration.DbPath}");

            var dataDriveInfo = new DriveInfo(dataPathRoot);
            var availableFreeSpace = (decimal)dataDriveInfo.AvailableFreeSpace;
            var totalSpace = (decimal)dataDriveInfo.TotalSize;

            var percentRemaining = (decimal)dataDriveInfo.AvailableFreeSpace / dataDriveInfo.TotalSize;
            logger.LogDebug("Free space: {AvailableFreeSpace:N0}B | Total: {TotalSpace:N0}B | Percent remaining {PercentRemaining:P0}", availableFreeSpace, totalSpace, percentRemaining);

            if (percentRemaining > percentageThreshold)
            {
                stateHolder.CanIngestMore = true;
                return SuccessResult;
            }

            logger.LogWarning("Audit message ingestion stopped! {PercentRemaining:P0} disk space remaining on data drive '{DataDriveInfoVolumeLabel} ({DataDriveInfoRootDirectory})' on '{EnvironmentMachineName}'. This is less than {PercentageThreshold}% - the minimal required space configured. The threshold can be set using the {RavenPersistenceConfigurationMinimumStorageLeftRequiredForIngestionKey} configuration setting", percentRemaining, dataDriveInfo.VolumeLabel, dataDriveInfo.RootDirectory, Environment.MachineName, percentageThreshold, RavenPersistenceConfiguration.MinimumStorageLeftRequiredForIngestionKey);
            stateHolder.CanIngestMore = false;
            return CheckResult.Failed($"Audit message ingestion stopped! {percentRemaining:P0} disk space remaining on data drive '{dataDriveInfo.VolumeLabel} ({dataDriveInfo.RootDirectory})' on '{Environment.MachineName}'. This is less than {percentageThreshold}% - the minimal required space configured. The threshold can be set using the {RavenPersistenceConfiguration.MinimumStorageLeftRequiredForIngestionKey} configuration setting.");
        }

        public static int Parse(IDictionary<string, string> settings)
        {
            if (!settings.TryGetValue(RavenPersistenceConfiguration.MinimumStorageLeftRequiredForIngestionKey, out var thresholdValue))
            {
                thresholdValue = $"{MinimumStorageLeftRequiredForIngestionDefault}";
            }

            if (!int.TryParse(thresholdValue, out var threshold))
            {
                Logger.LogCritical("{RavenPersistenceConfigurationMinimumStorageLeftRequiredForIngestionKey} must be an integer", RavenPersistenceConfiguration.MinimumStorageLeftRequiredForIngestionKey);
                throw new Exception($"{RavenPersistenceConfiguration.MinimumStorageLeftRequiredForIngestionKey} must be an integer.");
            }

            if (threshold < 0)
            {
                Logger.LogCritical("{RavenPersistenceConfigurationMinimumStorageLeftRequiredForIngestionKey} is invalid, minimum value is 0", RavenPersistenceConfiguration.MinimumStorageLeftRequiredForIngestionKey);
                throw new Exception($"{RavenPersistenceConfiguration.MinimumStorageLeftRequiredForIngestionKey} is invalid, minimum value is 0.");
            }

            if (threshold > 100)
            {
                Logger.LogCritical("{RavenPersistenceConfigurationMinimumStorageLeftRequiredForIngestionKey} is invalid, maximum value is 100", RavenPersistenceConfiguration.MinimumStorageLeftRequiredForIngestionKey);
                throw new Exception($"{RavenPersistenceConfiguration.MinimumStorageLeftRequiredForIngestionKey} is invalid, maximum value is 100.");
            }

            return threshold;
        }

        public const int MinimumStorageLeftRequiredForIngestionDefault = 5;
        static readonly Task<CheckResult> SuccessResult = Task.FromResult(CheckResult.Pass);
        static readonly ILogger<CheckMinimumStorageRequiredForIngestion> Logger = LoggerUtil.CreateStaticLogger<CheckMinimumStorageRequiredForIngestion>();
    }
}