namespace ServiceControl.Audit.Persistence.RavenDB.UnitOfWork
{
    using System.Threading;
    using System.Threading.Tasks;
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
        public async ValueTask<IAuditIngestionUnitOfWork> StartNew(int batchSize, CancellationToken cancellationToken)
        {
            var timedCancellationSource = new CancellationTokenSource(databaseConfiguration.BulkInsertCommitTimeout);
            var bulkInsert = (await documentStoreProvider.GetDocumentStore(timedCancellationSource.Token))
                .BulkInsert(new BulkInsertOptions { SkipOverwriteIfUnchanged = true, }, timedCancellationSource.Token);

            return new RavenAuditIngestionUnitOfWork(
                bulkInsert, timedCancellationSource, databaseConfiguration.AuditRetentionPeriod, new RavenAttachmentsBodyStorage(sessionProvider, bulkInsert, databaseConfiguration.MaxBodySizeToStore)
            );
        }

        public bool CanIngestMore() => customCheckState.CanIngestMore;
    }
}
