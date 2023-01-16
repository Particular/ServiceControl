namespace ServiceControl.Audit.Persistence.RavenDb.CustomChecks
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using NServiceBus.CustomChecks;
    using NServiceBus.Logging;
    using ServiceControl.Audit.Persistence.RavenDB;

    class CheckMinimumStorageRequiredForAuditIngestion : CustomCheck
    {
        public CheckMinimumStorageRequiredForAuditIngestion(State stateHolder, PersistenceSettings settings)
            : base("Audit Message Ingestion Process", "ServiceControl Health", TimeSpan.FromSeconds(5))
        {
            this.stateHolder = stateHolder;
            this.settings = settings;
        }

        public override Task<CheckResult> PerformCheck()
        {
            if (int.TryParse(settings.PersisterSpecificSettings[RavenBootstrapper.MinimumStorageLeftRequiredForIngestionKey], out var storageThreshold) == false)
            {
                stateHolder.CanIngestMore = true;
                return successResult;
            }

            var dataPathRoot = Path.GetPathRoot(settings.PersisterSpecificSettings[RavenBootstrapper.DatabasePathKey]);
            if (dataPathRoot == null)
            {
                stateHolder.CanIngestMore = true;
                return successResult;
            }

            var percentageThreshold = storageThreshold / 100m;

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

            var message = $"{percentRemaining:P0} disk space remaining on data drive '{dataDriveInfo.VolumeLabel} ({dataDriveInfo.RootDirectory})' on '{Environment.MachineName}'.";
            stateHolder.CanIngestMore = false;
            return CheckResult.Failed(message);
        }

        readonly State stateHolder;
        readonly PersistenceSettings settings;
        static Task<CheckResult> successResult = Task.FromResult(CheckResult.Pass);
        static readonly ILog Logger = LogManager.GetLogger(typeof(CheckMinimumStorageRequiredForAuditIngestion));

        public class State
        {
            public bool CanIngestMore { get; set; } = true;
        }
    }
}