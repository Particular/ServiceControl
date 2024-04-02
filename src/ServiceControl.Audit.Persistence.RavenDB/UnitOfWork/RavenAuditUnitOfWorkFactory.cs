namespace ServiceControl.Audit.Persistence.RavenDB.UnitOfWork
{
    using System;
    using System.Threading;
    using Persistence.UnitOfWork;
    using Raven.Client.Documents.BulkInsert;
    using RavenDB;
    using ServiceControl.Audit.Persistence.RavenDB.CustomChecks;

    class RavenAuditIngestionUnitOfWorkFactory : IAuditIngestionUnitOfWorkFactory
    {
        public RavenAuditIngestionUnitOfWorkFactory(IRavenDocumentStoreProvider documentStoreProvider, IRavenSessionProvider sessionProvider,
            DatabaseConfiguration databaseConfiguration, CheckMinimumStorageRequiredForAuditIngestion.State customCheckState)
        {
            this.documentStoreProvider = documentStoreProvider;
            this.sessionProvider = sessionProvider;
            this.databaseConfiguration = databaseConfiguration;
            this.customCheckState = customCheckState;
        }

        public IAuditIngestionUnitOfWork StartNew(int batchSize)
        {
            var timedCancellationSource = new CancellationTokenSource(databaseConfiguration.BulkInsertCommitTimeout);
            var bulkInsert = documentStoreProvider.GetDocumentStore()
                .BulkInsert(new BulkInsertOptions { SkipOverwriteIfUnchanged = true, }, timedCancellationSource.Token);

            return new RavenAuditIngestionUnitOfWork(
                bulkInsert, timedCancellationSource, databaseConfiguration.AuditRetentionPeriod, new RavenAttachmentsBodyStorage(sessionProvider, bulkInsert, databaseConfiguration.MaxBodySizeToStore)
            );
        }

        public bool CanIngestMore()
        {
            return customCheckState.CanIngestMore;
        }

        readonly IRavenDocumentStoreProvider documentStoreProvider;
        readonly IRavenSessionProvider sessionProvider;
        readonly DatabaseConfiguration databaseConfiguration;
        readonly CheckMinimumStorageRequiredForAuditIngestion.State customCheckState;
    }
}
