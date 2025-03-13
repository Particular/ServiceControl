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
            // DO NOT USE using var, will be disposed by RavenAuditIngestionUnitOfWork
            var lifetimeForwardedTimedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            lifetimeForwardedTimedCancellationSource.CancelAfter(databaseConfiguration.BulkInsertCommitTimeout);
            var bulkInsert = (await documentStoreProvider.GetDocumentStore(lifetimeForwardedTimedCancellationSource.Token))
                .BulkInsert(new BulkInsertOptions { SkipOverwriteIfUnchanged = true, }, lifetimeForwardedTimedCancellationSource.Token);

            return new RavenAuditIngestionUnitOfWork(
                bulkInsert,
                lifetimeForwardedTimedCancellationSource, // Transfer ownership for disposal
                databaseConfiguration.AuditRetentionPeriod,
                new RavenAttachmentsBodyStorage(sessionProvider, bulkInsert, databaseConfiguration.MaxBodySizeToStore)
            );
            // Intentionally not disposing CTS!
        }

        public bool CanIngestMore() => customCheckState.CanIngestMore;
    }
}
