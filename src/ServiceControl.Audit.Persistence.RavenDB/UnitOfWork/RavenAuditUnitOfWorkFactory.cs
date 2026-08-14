namespace ServiceControl.Audit.Persistence.RavenDB.UnitOfWork
{
    using System.Threading;
    using System.Threading.Tasks;
    using AuditRetentionBuckets;
    using Persistence.UnitOfWork;
    using Raven.Client.Documents.BulkInsert;
    using RavenDB;

    class RavenAuditIngestionUnitOfWorkFactory(
        IRavenDocumentStoreProvider documentStoreProvider,
        IRavenSessionProvider sessionProvider,
        DatabaseConfiguration databaseConfiguration,
        MinimumRequiredStorageState customCheckState,
        AuditRetentionBucketManager auditRetentionBucketManager)
        : IAuditIngestionUnitOfWorkFactory
    {
        public async ValueTask<IAuditIngestionUnitOfWork> StartNew(int batchSize, CancellationToken cancellationToken = default)
        {
            // DO NOT USE using var, will be disposed by RavenAuditIngestionUnitOfWork
            var lifetimeForwardedTimedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            lifetimeForwardedTimedCancellationSource.CancelAfter(databaseConfiguration.BulkInsertCommitTimeout);

            AuditRetentionBucket currentBucket = null;
            if (databaseConfiguration.EnableAuditRetentionBuckets)
            {
                currentBucket = await auditRetentionBucketManager.EnsureCurrentBucket(cancellationToken);
            }

            var bulkInsert = (await documentStoreProvider.GetDocumentStore(lifetimeForwardedTimedCancellationSource.Token))
                .BulkInsert(new BulkInsertOptions { SkipOverwriteIfUnchanged = true, }, lifetimeForwardedTimedCancellationSource.Token);

            return new RavenAuditIngestionUnitOfWork(
                bulkInsert,
                lifetimeForwardedTimedCancellationSource, // Transfer ownership for disposal
                databaseConfiguration.AuditRetentionPeriod,
                new RavenAttachmentsBodyStorage(sessionProvider, bulkInsert, databaseConfiguration.MaxBodySizeToStore),
                currentBucket
            );
            // Intentionally not disposing CTS!
        }

        public bool CanIngestMore() => customCheckState.CanIngestMore;
    }
}
