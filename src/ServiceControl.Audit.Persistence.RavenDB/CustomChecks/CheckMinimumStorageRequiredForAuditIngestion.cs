﻿namespace ServiceControl.Audit.Persistence.RavenDB.CustomChecks
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.CustomChecks;
    using NServiceBus.Logging;
    using ServiceControl.Audit.Persistence.RavenDB;

    class CheckMinimumStorageRequiredForAuditIngestion : CustomCheck
    {
        public CheckMinimumStorageRequiredForAuditIngestion(State stateHolder, DatabaseConfiguration databaseConfiguration)
            : base("Audit Message Ingestion Process", "ServiceControl.Audit Health", TimeSpan.FromSeconds(5))
        {
            this.stateHolder = stateHolder;
            this.databaseConfiguration = databaseConfiguration;
        }

        public override Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
        {
            var percentageThreshold = databaseConfiguration.MinimumStorageLeftRequiredForIngestion / 100m;

            var dataPathRoot = Path.GetPathRoot(databaseConfiguration.ServerConfiguration.DbPath);
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

            var message = $"Audit message ingestion stopped! {percentRemaining:P0} disk space remaining on data drive '{dataDriveInfo.VolumeLabel} ({dataDriveInfo.RootDirectory})' on '{Environment.MachineName}'. This is less than {percentageThreshold}% - the minimal required space configured. The threshold can be set using the {RavenPersistenceConfiguration.MinimumStorageLeftRequiredForIngestionKey} configuration setting.";
            Logger.Warn(message);
            stateHolder.CanIngestMore = false;
            return CheckResult.Failed(message);
        }

        readonly State stateHolder;
        readonly DatabaseConfiguration databaseConfiguration;
        static Task<CheckResult> successResult = Task.FromResult(CheckResult.Pass);
        static readonly ILog Logger = LogManager.GetLogger(typeof(CheckMinimumStorageRequiredForAuditIngestion));

        public class State
        {
            public bool CanIngestMore { get; set; } = true;
        }
    }
}