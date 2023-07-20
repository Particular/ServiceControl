namespace ServiceControl.Operations
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using NServiceBus.CustomChecks;
    using NServiceBus.Logging;
    using Persistence.RavenDb;
    using ServiceControl.Persistence;

    class CheckMinimumStorageRequiredForIngestion : CustomCheck
    {
        public CheckMinimumStorageRequiredForIngestion(MinimumRequiredStorageState stateHolder, PersistenceSettings settings)
            : base("Message Ingestion Process", "ServiceControl Health", TimeSpan.FromSeconds(5))
        {
            this.stateHolder = stateHolder;

            dataPathRoot = Path.GetPathRoot(settings.PersisterSpecificSettings[RavenDbPersistenceConfiguration.DbPathKey]);
            percentageThreshold = GetMinimumStorageLeftRequiredForIngestion(settings) / 100m;
        }

        public override Task<CheckResult> PerformCheck()
        {
            if (dataPathRoot == null)
            {
                stateHolder.CanIngestMore = true;
                return successResult;
            }

            Logger.Debug($"Check ServiceControl data drive space starting. Threshold {percentageThreshold:P0}");

            var dataDriveInfo = new DriveInfo(dataPathRoot);
            var availableFreeSpace = (decimal)dataDriveInfo.AvailableFreeSpace;
            var totalSpace = (decimal)dataDriveInfo.TotalSize;

            var percentRemaining = (decimal)dataDriveInfo.AvailableFreeSpace / dataDriveInfo.TotalSize;

            if (Logger.IsDebugEnabled)
            {
                Logger.Debug($"Free space: {availableFreeSpace} | Total: {totalSpace} | Percent remaining {percentRemaining:P0}");
            }

            if (percentRemaining > percentageThreshold)
            {
                stateHolder.CanIngestMore = true;
                return successResult;
            }

            var message = $"Error message ingestion stopped! {percentRemaining:P0} disk space remaining on data drive '{dataDriveInfo.VolumeLabel} ({dataDriveInfo.RootDirectory})' on '{Environment.MachineName}'. This is less than {percentageThreshold}% - the minimal required space configured. The threshold can be set using the {RavenBootstrapper.MinimumStorageLeftRequiredForIngestionKey} configuration setting.";
            Logger.Warn(message);
            stateHolder.CanIngestMore = false;
            return CheckResult.Failed(message);
        }

        int GetMinimumStorageLeftRequiredForIngestion(PersistenceSettings settings)
        {
            int threshold = MinimumStorageLeftRequiredForIngestionDefault;

            if (settings.PersisterSpecificSettings.TryGetValue(RavenBootstrapper.MinimumStorageLeftRequiredForIngestionKey, out var thresholdValue))
            {
                threshold = int.Parse(thresholdValue);
            }

            string message;
            if (threshold < 0)
            {
                message = $"{RavenBootstrapper.MinimumStorageLeftRequiredForIngestionKey} is invalid, minimum value is 0.";
                Logger.Fatal(message);
                throw new Exception(message);
            }

            if (threshold > 100)
            {
                message = $"{RavenBootstrapper.MinimumStorageLeftRequiredForIngestionKey} is invalid, maximum value is 100.";
                Logger.Fatal(message);
                throw new Exception(message);
            }

            return threshold;
        }

        const int MinimumStorageLeftRequiredForIngestionDefault = 5;

        readonly MinimumRequiredStorageState stateHolder;
        readonly string dataPathRoot;
        readonly decimal percentageThreshold;

        static Task<CheckResult> successResult = Task.FromResult(CheckResult.Pass);
        static readonly ILog Logger = LogManager.GetLogger(typeof(CheckMinimumStorageRequiredForIngestion));
    }
}