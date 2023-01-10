﻿namespace ServiceControl.Audit.Persistence.RavenDb.UnitOfWork
{
    using System;
    using System.IO;
    using Persistence.UnitOfWork;
    using Raven.Client.Documents.BulkInsert;
    using RavenDb;
    using NServiceBus.Logging;
    using NServiceBus.CustomChecks;
    using System.Threading.Tasks;

    class AuditStorageCustomCheck : CustomCheck
    {
        public AuditStorageCustomCheck(State stateHolder, DatabaseConfiguration databaseConfiguration)
            : base("Audit Message Ingestion Process", "ServiceControl Health", TimeSpan.FromSeconds(5))
        {
            this.stateHolder = stateHolder;
            this.databaseConfiguration = databaseConfiguration;
        }

        public override Task<CheckResult> PerformCheck()
        {
            var percentageThreshold = databaseConfiguration.CriticalDataSpaceRemainingThreshold / 100m;

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

            var message = $"{percentRemaining:P0} disk space remaining on data drive '{dataDriveInfo.VolumeLabel} ({dataDriveInfo.RootDirectory})' on '{Environment.MachineName}'.";
            stateHolder.CanIngestMore = false;
            return CheckResult.Failed(message);
        }

        readonly State stateHolder;
        private readonly DatabaseConfiguration databaseConfiguration;
        static Task<CheckResult> successResult = Task.FromResult(CheckResult.Pass);
        static readonly ILog Logger = LogManager.GetLogger(typeof(AuditStorageCustomCheck));

        public class State
        {
            public bool CanIngestMore { get; set; } = true;
        }
    }

    class RavenDbAuditIngestionUnitOfWorkFactory : IAuditIngestionUnitOfWorkFactory
    {
        public RavenDbAuditIngestionUnitOfWorkFactory(IRavenDbDocumentStoreProvider documentStoreProvider, IRavenDbSessionProvider sessionProvider,
            DatabaseConfiguration databaseConfiguration, AuditStorageCustomCheck.State customCheckState)
        {
            this.documentStoreProvider = documentStoreProvider;
            this.sessionProvider = sessionProvider;
            this.databaseConfiguration = databaseConfiguration;
            this.customCheckState = customCheckState;
        }

        public IAuditIngestionUnitOfWork StartNew(int batchSize)
        {
            var bulkInsert = documentStoreProvider.GetDocumentStore()
                .BulkInsert(
                options: new BulkInsertOptions { SkipOverwriteIfUnchanged = true, });

            return new RavenDbAuditIngestionUnitOfWork(
                bulkInsert, databaseConfiguration.AuditRetentionPeriod, new RavenAttachmentsBodyStorage(sessionProvider, bulkInsert, databaseConfiguration.MaxBodySizeToStore)
            );
        }

        public bool CanIngestMore()
        {
            return customCheckState.CanIngestMore;
        }

        readonly IRavenDbDocumentStoreProvider documentStoreProvider;
        readonly IRavenDbSessionProvider sessionProvider;
        readonly DatabaseConfiguration databaseConfiguration;
        private readonly AuditStorageCustomCheck.State customCheckState;
    }
}
