namespace ServiceControl.Persistence.RavenDB.CustomChecks
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.CustomChecks;
    using NServiceBus.Logging;
    using ServiceControl.Persistence;
    using ServiceControl.Persistence.RavenDB;

    class CheckMinimumStorageRequiredForIngestion(MinimumRequiredStorageState stateHolder, RavenPersisterSettings settings) : CustomCheck("Message Ingestion Process", "ServiceControl Health", TimeSpan.FromSeconds(5))
    {
        public override Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
        {
            var percentageThreshold = settings.MinimumStorageLeftRequiredForIngestion / 100m;

            if (Logger.IsDebugEnabled)
            {
                Logger.Debug($"Check ServiceControl data drive space starting. Threshold {percentageThreshold:P0}");
            }

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

            if (Logger.IsDebugEnabled)
            {
                Logger.Debug($"Free space: {availableFreeSpace:N0}B | Total: {totalSpace:N0}B | Percent remaining {percentRemaining:P1}");
            }

            if (percentRemaining > percentageThreshold)
            {
                stateHolder.CanIngestMore = true;
                return SuccessResult;
            }

            var message = $"Error message ingestion stopped! {percentRemaining:P0} disk space remaining on data drive '{dataDriveInfo.VolumeLabel} ({dataDriveInfo.RootDirectory})' on '{Environment.MachineName}'. This is less than {percentageThreshold}% - the minimal required space configured. The threshold can be set using the {RavenBootstrapper.MinimumStorageLeftRequiredForIngestionKey} configuration setting.";
            Logger.Warn(message);
            stateHolder.CanIngestMore = false;
            return CheckResult.Failed(message);
        }

        public static void Validate(RavenPersisterSettings settings)
        {
            var threshold = settings.MinimumStorageLeftRequiredForIngestion;

            if (threshold < 0)
            {
                var message = $"{RavenBootstrapper.MinimumStorageLeftRequiredForIngestionKey} is invalid, minimum value is 0.";
                Logger.Fatal(message);
                throw new Exception(message);
            }

            if (threshold > 100)
            {
                var message = $"{RavenBootstrapper.MinimumStorageLeftRequiredForIngestionKey} is invalid, maximum value is 100.";
                Logger.Fatal(message);
                throw new Exception(message);
            }
        }

        public const int MinimumStorageLeftRequiredForIngestionDefault = 5;
        static readonly Task<CheckResult> SuccessResult = Task.FromResult(CheckResult.Pass);
        static readonly ILog Logger = LogManager.GetLogger(typeof(CheckMinimumStorageRequiredForIngestion));
    }
}