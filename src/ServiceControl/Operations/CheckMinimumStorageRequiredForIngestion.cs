namespace ServiceControl.Operations
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using NServiceBus.CustomChecks;
    using NServiceBus.Logging;
    using ServiceBus.Management.Infrastructure.Settings;

    class CheckMinimumStorageRequiredForIngestion : CustomCheck
    {
        public CheckMinimumStorageRequiredForIngestion(State stateHolder, Settings settings)
            : base("Message Ingestion Process", "ServiceControl Health", TimeSpan.FromSeconds(5))
        {
            this.stateHolder = stateHolder;
            this.settings = settings;
        }

        public override Task<CheckResult> PerformCheck()
        {
            var percentageThreshold = settings.MinimumStorageLeftRequiredForIngestion / 100m;

            var dataPathRoot = Path.GetPathRoot(settings.DbPath);
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

            var message = $"Error message ingestion stopped! {percentRemaining:P0} disk space remaining on data drive '{dataDriveInfo.VolumeLabel} ({dataDriveInfo.RootDirectory})' on '{Environment.MachineName}'. This is less than {percentageThreshold}% - the minimal required space configured. The threshold can be set using the {nameof(Settings.MinimumStorageLeftRequiredForIngestion)} configuration setting.";
            stateHolder.CanIngestMore = false;
            return CheckResult.Failed(message);
        }

        readonly State stateHolder;
        readonly Settings settings;
        static Task<CheckResult> successResult = Task.FromResult(CheckResult.Pass);
        static readonly ILog Logger = LogManager.GetLogger(typeof(CheckMinimumStorageRequiredForIngestion));

        public class State
        {
            public bool CanIngestMore { get; set; } = true;
        }
    }
}