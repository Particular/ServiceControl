namespace ServiceControl.Persistence.RavenDB.CustomChecks
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.CustomChecks;
    using NServiceBus.Logging;
    using ServiceControl.Persistence.RavenDB;

    class CheckFreeDiskSpace(RavenPersisterSettings settings)
        : CustomCheck("ServiceControl database", "Storage space", TimeSpan.FromMinutes(5))
    {
        public override Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
        {
            if (Logger.IsDebugEnabled)
            {
                Logger.Debug($"Check ServiceControl data drive space remaining custom check starting. Threshold {percentageThreshold:P0}");
            }

            if (!settings.UseEmbeddedServer)
            {
                return CheckResult.Pass;
            }

            if (dataPathRoot == null)
            {
                throw new Exception($"Unable to find the root of the data path {dataPathRoot}");
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

        readonly string dataPathRoot = Path.GetPathRoot(settings.DatabasePath);
        readonly decimal percentageThreshold = settings.DataSpaceRemainingThreshold / 100m;

        public const int DataSpaceRemainingThresholdDefault = 20;
        static readonly ILog Logger = LogManager.GetLogger(typeof(CheckFreeDiskSpace));
    }
}