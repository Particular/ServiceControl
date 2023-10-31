namespace ServiceControl.Operations
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using NServiceBus.CustomChecks;
    using NServiceBus.Logging;
    using Persistence.RavenDB;

    class CheckFreeDiskSpace : CustomCheck
    {
        public CheckFreeDiskSpace(RavenPersisterSettings settings) : base("ServiceControl database", "Storage space", TimeSpan.FromMinutes(5))
        {
            dataPath = settings.DatabasePath;
            percentageThreshold = settings.DataSpaceRemainingThreshold;

            Logger.Debug($"Check ServiceControl data drive space remaining custom check starting. Threshold {percentageThreshold:P0}");
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
                Logger.Debug($"Free space: {availableFreeSpace:N0}B | Total: {totalSpace:N0}B | Percent remaining {percentRemaining:P1}");
            }

            return percentRemaining > percentageThreshold
                ? CheckResult.Pass
                : CheckResult.Failed($"{percentRemaining:P0} disk space remaining on data drive '{dataDriveInfo.VolumeLabel} ({dataDriveInfo.RootDirectory})' on '{Environment.MachineName}'.");
        }

        public static void Validate(RavenPersisterSettings settings)
        {
            var threshold = settings.DataSpaceRemainingThreshold;

            string message;

            if (threshold < 0)
            {
                message = $"{RavenPersistenceConfiguration.DataSpaceRemainingThresholdKey} is invalid, minimum value is 0.";
                Logger.Fatal(message);
                throw new Exception(message);
            }

            if (threshold > 100)
            {
                message = $"{RavenPersistenceConfiguration.DataSpaceRemainingThresholdKey} is invalid, maximum value is 100.";
                Logger.Fatal(message);
                throw new Exception(message);
            }
        }

        readonly string dataPath;
        readonly decimal percentageThreshold;

        public const int DataSpaceRemainingThresholdDefault = 20;
        static readonly ILog Logger = LogManager.GetLogger(typeof(CheckFreeDiskSpace));
    }
}