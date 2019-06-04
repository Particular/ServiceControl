namespace ServiceControl.Operations
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using NServiceBus.CustomChecks;
    using NServiceBus.Logging;
    using ServiceBus.Management.Infrastructure.Settings;

    class CheckFreeDiskSpace : CustomCheck
    {
        public CheckFreeDiskSpace(Settings settings) : base("ServiceControl database", "Storage space", TimeSpan.FromSeconds(300))
        {
            Logger.Debug($"Check ServiceControl data drive space remaining custom check starting. Threshold {PercentageThreshold:P0}");
            dataPath = settings.DbPath;
        }

        public override Task<CheckResult> PerformCheck()
        {
            var dataPathRoot = Path.GetPathRoot(dataPath);

            if (dataPathRoot == null)
            {
                throw new Exception($"Unable to find the root of the data path {dataPath}");
            }

            var dataDriveInfo = new DriveInfo(dataPathRoot);
            var availableFreeSpace = (decimal)dataDriveInfo.AvailableFreeSpace;
            var totalSpace = (decimal)dataDriveInfo.TotalSize;

            var percentRemaining = (decimal)dataDriveInfo.AvailableFreeSpace / dataDriveInfo.TotalSize;

            if (Logger.IsDebugEnabled)
            {
                Logger.Debug($"Free space: {availableFreeSpace} | Total: {totalSpace} | Percent remaining {percentRemaining:P0}");
            }

            return percentRemaining > PercentageThreshold
                ? CheckResult.Pass
                : CheckResult.Failed($"{percentRemaining:P0} disk space remaining on data drive '{dataDriveInfo.VolumeLabel} ({dataDriveInfo.RootDirectory})' on '{Environment.MachineName}'.");
        }

        readonly string dataPath;
        const decimal PercentageThreshold = 20m / 100m;
        static readonly ILog Logger = LogManager.GetLogger(typeof(CheckFreeDiskSpace));
    }
}