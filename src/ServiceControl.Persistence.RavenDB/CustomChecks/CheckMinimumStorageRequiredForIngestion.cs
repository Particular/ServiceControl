namespace ServiceControl.Persistence.RavenDB.CustomChecks
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using NServiceBus.CustomChecks;
    using ServiceControl.Infrastructure;
    using ServiceControl.Persistence;
    using ServiceControl.Persistence.RavenDB;

    class CheckMinimumStorageRequiredForIngestion(MinimumRequiredStorageState stateHolder, RavenPersisterSettings settings, ILogger<CheckMinimumStorageRequiredForIngestion> logger) : CustomCheck("Message Ingestion Process", "ServiceControl Health", TimeSpan.FromSeconds(5))
    {
        public override Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
        {
            var percentageThreshold = settings.MinimumStorageLeftRequiredForIngestion / 100m;

            logger.LogDebug("Check ServiceControl data drive space starting. Threshold {PercentageThreshold:P0}", percentageThreshold);

            // Should be checking UseEmbeddedServer but need to check DatabasePath instead for the ATT hack to work
            if (string.IsNullOrEmpty(settings.DatabasePath))
            {
                stateHolder.CanIngestMore = true;
                return SuccessResult;
            }

            var dataPathRoot = Path.GetPathRoot(settings.DatabasePath) ?? throw new Exception($"Unable to find the root of the data path {settings.DatabasePath}");

            var dataDriveInfo = new DriveInfo(dataPathRoot);
            var availableFreeSpace = (decimal)dataDriveInfo.AvailableFreeSpace;
            var totalSpace = (decimal)dataDriveInfo.TotalSize;

            var percentRemaining = (decimal)dataDriveInfo.AvailableFreeSpace / dataDriveInfo.TotalSize;

            logger.LogDebug("Free space: {FreeSpaceTotalBytesFree:N0}B | Total: {FreeSpaceTotalBytesAvailable:N0}B | Remaining {PercentRemaining:P1}%", availableFreeSpace, totalSpace, percentRemaining);

            if (percentRemaining > percentageThreshold)
            {
                stateHolder.CanIngestMore = true;
                return SuccessResult;
            }

            logger.LogWarning("Error message ingestion stopped! {PercentRemaining:P0} disk space remaining on data drive '{DataDriveInfoVolumeLabel} ({DataDriveInfoRootDirectory})' on '{MachineName}'. This is less than {PercentageThreshold}% - the minimal required space configured. The threshold can be set using the {RavenBootstrapperMinimumStorageLeftRequiredForIngestionKey} configuration setting",
                percentRemaining,
                dataDriveInfo.VolumeLabel,
                dataDriveInfo.RootDirectory,
                Environment.MachineName,
                percentageThreshold,
                RavenPersistenceConfiguration.MinimumStorageLeftRequiredForIngestionKey);
            stateHolder.CanIngestMore = false;
            return CheckResult.Failed($"Error message ingestion stopped! {percentRemaining:P0} disk space remaining on data drive '{dataDriveInfo.VolumeLabel} ({dataDriveInfo.RootDirectory})' on '{Environment.MachineName}'. This is less than {percentageThreshold}% - the minimal required space configured. The threshold can be set using the {RavenPersistenceConfiguration.MinimumStorageLeftRequiredForIngestionKey} configuration setting.");
        }

        public const int MinimumStorageLeftRequiredForIngestionDefault = 5;
        static readonly Task<CheckResult> SuccessResult = Task.FromResult(CheckResult.Pass);


        public sealed class Validation : IValidateOptions<RavenPersisterSettings>          // TODO: Register!!
        {
            public ValidateOptionsResult Validate(string name, RavenPersisterSettings options)
            {
                var threshold = options.MinimumStorageLeftRequiredForIngestion;

                if (threshold < 0)
                {
                    return ValidateOptionsResult.Fail($"{RavenPersistenceConfiguration.MinimumStorageLeftRequiredForIngestionKey} is invalid, minimum value is 0.");
                }

                if (threshold > 100)
                {
                    return ValidateOptionsResult.Fail($"{RavenPersistenceConfiguration.MinimumStorageLeftRequiredForIngestionKey} is invalid, maximum value is 100.");
                }

                return ValidateOptionsResult.Success;
            }
        }
    }
}