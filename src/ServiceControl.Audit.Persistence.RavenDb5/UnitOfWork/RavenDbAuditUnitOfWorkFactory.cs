namespace ServiceControl.Audit.Persistence.RavenDb.UnitOfWork
{
    using System;
    using System.Threading;
    using Persistence.UnitOfWork;
    using Raven.Client.Documents.BulkInsert;
    using RavenDb;
    using ServiceControl.Audit.Persistence.RavenDb5.CustomChecks;

    class RavenDbAuditIngestionUnitOfWorkFactory : IAuditIngestionUnitOfWorkFactory
    {
        public RavenDbAuditIngestionUnitOfWorkFactory(IRavenDbDocumentStoreProvider documentStoreProvider, IRavenDbSessionProvider sessionProvider,
            DatabaseConfiguration databaseConfiguration, CheckMinimumStorageRequiredForAuditIngestion.State customCheckState)
        {
            this.documentStoreProvider = documentStoreProvider;
            this.sessionProvider = sessionProvider;
            this.databaseConfiguration = databaseConfiguration;
            this.customCheckState = customCheckState;
        }

        public IAuditIngestionUnitOfWork StartNew(int batchSize)
        {
            var timedCancellation = new CancellationTokenSource(TimeSpan.FromMinutes(1));
            var bulkInsert = documentStoreProvider.GetDocumentStore()
                .BulkInsert(new BulkInsertOptions { SkipOverwriteIfUnchanged = true, }, timedCancellation.Token);

            return new RavenDbAuditIngestionUnitOfWork(
                bulkInsert, timedCancellation, databaseConfiguration.AuditRetentionPeriod, new RavenAttachmentsBodyStorage(sessionProvider, bulkInsert, databaseConfiguration.MaxBodySizeToStore)
            );
        }

        public bool CanIngestMore()
        {
            return customCheckState.CanIngestMore;
        }

        readonly IRavenDbDocumentStoreProvider documentStoreProvider;
        readonly IRavenDbSessionProvider sessionProvider;
        readonly DatabaseConfiguration databaseConfiguration;
        readonly CheckMinimumStorageRequiredForAuditIngestion.State customCheckState;
    }
}
