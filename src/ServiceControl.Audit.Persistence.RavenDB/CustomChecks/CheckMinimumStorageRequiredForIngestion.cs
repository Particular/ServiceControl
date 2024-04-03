namespace ServiceControl.Audit.Persistence.RavenDB.CustomChecks
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.CustomChecks;
    using NServiceBus.Logging;
    using RavenDB;

    class CheckMinimumStorageRequiredForIngestion : CustomCheck
    {
        public CheckMinimumStorageRequiredForIngestion(MinimumRequiredStorageState stateHolder, DatabaseConfiguration databaseConfiguration) : base("Audit Message Ingestion Process", "ServiceControl.Audit Health", TimeSpan.FromSeconds(5))
        {
            this.stateHolder = stateHolder;
            this.databaseConfiguration = databaseConfiguration;
            dataPathRoot = Path.GetPathRoot(databaseConfiguration.ServerConfiguration.DbPath);
            percentageThreshold = this.databaseConfiguration.MinimumStorageLeftRequiredForIngestion / 100m;
        }

        public override Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
        {
            if (Logger.IsDebugEnabled)
            {
                Logger.Debug($"Check ServiceControl data drive space starting. Threshold {percentageThreshold:P0}");
            }

            if (!databaseConfiguration.ServerConfiguration.UseEmbeddedServer)
            {
                stateHolder.CanIngestMore = true;
                return CheckResult.Pass;
            }

            if (dataPathRoot is null)
            {
                throw new Exception($"Unable to find the root of the data path {databaseConfiguration.ServerConfiguration.DbPath}");
            }

            var dataDriveInfo = new DriveInfo(dataPathRoot);
            var availableFreeSpace = (decimal)dataDriveInfo.AvailableFreeSpace;
            var totalSpace = (decimal)dataDriveInfo.TotalSize;

            var percentRemaining = (decimal)dataDriveInfo.AvailableFreeSpace / dataDriveInfo.TotalSize;

            if (Logger.IsDebugEnabled)
            {
                Logger.Debug(
                    $"Free space: {availableFreeSpace} | Total: {totalSpace} | Percent remaining {percentRemaining:P0}");
            }

            if (percentRemaining > percentageThreshold)
            {
                stateHolder.CanIngestMore = true;
                return CheckResult.Pass;
            }

            var message =
                $"Audit message ingestion stopped! {percentRemaining:P0} disk space remaining on data drive '{dataDriveInfo.VolumeLabel} ({dataDriveInfo.RootDirectory})' on '{Environment.MachineName}'. This is less than {percentageThreshold}% - the minimal required space configured. The threshold can be set using the {RavenPersistenceConfiguration.MinimumStorageLeftRequiredForIngestionKey} configuration setting.";
            Logger.Warn(message);
            stateHolder.CanIngestMore = false;
            return CheckResult.Failed(message);
        }

        public static int Parse(IDictionary<string, string> settings)
        {
            if (!settings.TryGetValue(RavenPersistenceConfiguration.MinimumStorageLeftRequiredForIngestionKey,
                    out var thresholdValue))
            {
                thresholdValue = $"{MinimumStorageLeftRequiredForIngestionDefault}";
            }

            string message;
            if (!int.TryParse(thresholdValue, out var threshold))
            {
                message =
                    $"{RavenPersistenceConfiguration.MinimumStorageLeftRequiredForIngestionKey} must be an integer.";
                Logger.Fatal(message);
                throw new Exception(message);
            }

            if (threshold < 0)
            {
                message =
                    $"{RavenPersistenceConfiguration.MinimumStorageLeftRequiredForIngestionKey} is invalid, minimum value is 0.";
                Logger.Fatal(message);
                throw new Exception(message);
            }

            if (threshold > 100)
            {
                message =
                    $"{RavenPersistenceConfiguration.MinimumStorageLeftRequiredForIngestionKey} is invalid, maximum value is 100.";
                Logger.Fatal(message);
                throw new Exception(message);
            }

            return threshold;
        }

        public const int MinimumStorageLeftRequiredForIngestionDefault = 5;

        readonly string dataPathRoot;
        readonly decimal percentageThreshold;
        readonly MinimumRequiredStorageState stateHolder;
        readonly DatabaseConfiguration databaseConfiguration;

        static readonly ILog Logger = LogManager.GetLogger(typeof(CheckMinimumStorageRequiredForIngestion));
    }
}