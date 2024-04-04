namespace ServiceControl.Audit.Persistence.RavenDB.UnitOfWork
{
    using System;
    using System.Threading;
    using Persistence.UnitOfWork;
    using Raven.Client.Documents.BulkInsert;
    using RavenDB;

    class RavenAuditIngestionUnitOfWorkFactory(
        IRavenDocumentStoreProvider documentStoreProvider,
        IRavenSessionProvider sessionProvider,
        DatabaseConfiguration databaseConfiguration,
        MinimumRequiredStorageState customCheckState)
        : IAuditIngestionUnitOfWorkFactory
    {
        public IAuditIngestionUnitOfWork StartNew(int batchSize)
        {
            var timedCancellationSource = new CancellationTokenSource(databaseConfiguration.BulkInsertCommitTimeout);
            var bulkInsert = documentStoreProvider.GetDocumentStore()
                .BulkInsert(new BulkInsertOptions { SkipOverwriteIfUnchanged = true, }, timedCancellationSource.Token);

            return new RavenAuditIngestionUnitOfWork(
                bulkInsert, timedCancellationSource, databaseConfiguration.AuditRetentionPeriod, new RavenAttachmentsBodyStorage(sessionProvider, bulkInsert, databaseConfiguration.MaxBodySizeToStore)
            );
        }

        public bool CanIngestMore() => customCheckState.CanIngestMore;
    }
}
