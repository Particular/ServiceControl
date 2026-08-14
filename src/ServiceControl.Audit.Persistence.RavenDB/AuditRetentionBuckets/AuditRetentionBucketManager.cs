namespace ServiceControl.Audit.Persistence.RavenDB.AuditRetentionBuckets
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Indexes;
    using Raven.Client.Documents.Operations;
    using Raven.Client.Documents.Operations.Indexes;
    using Raven.Client.Documents.Queries;
    using ServiceControl.Infrastructure;

    /// <summary>
    /// Owns the retention bucket catalog for a single Audit instance. Bucket mode is opt-in and
    /// disabled by default; when disabled this class is never invoked.
    ///
    /// Lifecycle:
    ///  - <see cref="EnsureCurrentBucket"/> is called on every ingestion batch start and creates the
    ///    current UTC bucket (catalog entry first, then its dedicated static indexes) on rollover.
    ///  - <see cref="GetActiveBuckets"/> is called by every read and only returns buckets that are
    ///    still visible through the catalog.
    ///  - <see cref="RunCleanup"/> retires expired buckets through the catalog, deletes their dedicated
    ///    indexes, deletes their collections and finally removes them from the catalog. Every step is
    ///    idempotent so an interrupted cleanup is resumed on the next run or after a restart.
    ///
    /// All bucket selection and retention decisions use the injected <see cref="TimeProvider"/> instead
    /// of the wall clock so production can use <see cref="TimeProvider.System"/> and tests can drive
    /// rollover and cleanup deterministically with a controllable provider.
    /// </summary>
    class AuditRetentionBucketManager(
        IRavenDocumentStoreProvider documentStoreProvider,
        DatabaseConfiguration configuration,
        TimeProvider timeProvider,
        ILogger<AuditRetentionBucketManager> logger)
    {
        public async Task<AuditRetentionBucket> EnsureCurrentBucket(CancellationToken cancellationToken = default)
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                await InitializeCore(cancellationToken);

                var currentKey = AuditRetentionBucketNaming.GetBucketKey(AuditRetentionBucketNaming.GetBucketStart(UtcNow(), DatabaseConfiguration.AuditRetentionBucketDuration));
                if (currentBucket == null || currentBucket.Key != currentKey)
                {
                    currentBucket = catalog.Buckets.FirstOrDefault(b => b.Key == currentKey && b.State == AuditRetentionBucketState.Active)
                        ?? await CreateBucket(currentKey, cancellationToken);
                }

                return currentBucket;
            }
            finally
            {
                gate.Release();
            }
        }

        public async Task<IReadOnlyList<AuditRetentionBucket>> GetActiveBuckets(CancellationToken cancellationToken = default)
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                await InitializeCore(cancellationToken);
                return activeBuckets;
            }
            finally
            {
                gate.Release();
            }
        }

        public async Task RunCleanup(CancellationToken cancellationToken = default)
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                await InitializeCore(cancellationToken);
                await CleanupExpiredBuckets(await documentStoreProvider.GetDocumentStore(cancellationToken), cancellationToken);
            }
            finally
            {
                gate.Release();
            }
        }

        async Task InitializeCore(CancellationToken cancellationToken)
        {
            if (initialized)
            {
                return;
            }

            var documentStore = await documentStoreProvider.GetDocumentStore(cancellationToken);

            catalog = await LoadOrCreateCatalog(documentStore, cancellationToken);

            // Restart recovery: every active bucket must have its dedicated static indexes. A bucket
            // whose catalog entry was persisted before its indexes were created (interrupted rollover)
            // is completed here.
            foreach (var bucket in catalog.Buckets.Where(b => b.State == AuditRetentionBucketState.Active))
            {
                await EnsureBucketIndexes(documentStore, bucket, cancellationToken);
            }

            RefreshActiveBuckets();
            initialized = true;
        }

        async Task<AuditRetentionBucket> CreateBucket(string bucketKey, CancellationToken cancellationToken)
        {
            var documentStore = await documentStoreProvider.GetDocumentStore(cancellationToken);
            var bucketStart = AuditRetentionBucketNaming.GetBucketStart(UtcNow(), DatabaseConfiguration.AuditRetentionBucketDuration);

            var bucket = new AuditRetentionBucket
            {
                Key = bucketKey,
                Start = bucketStart,
                End = bucketStart.Add(DatabaseConfiguration.AuditRetentionBucketDuration),
                State = AuditRetentionBucketState.Active,
                ProcessedMessageCollection = AuditRetentionBucketNaming.GetProcessedMessageCollection(bucketKey),
                SagaSnapshotCollection = AuditRetentionBucketNaming.GetSagaSnapshotCollection(bucketKey),
                MessagesViewIndex = AuditRetentionBucketNaming.GetMessagesViewIndexName(bucketKey),
                MessagesViewFullTextIndex = AuditRetentionBucketNaming.GetMessagesViewFullTextIndexName(bucketKey),
                SagaDetailsIndex = AuditRetentionBucketNaming.GetSagaDetailsIndexName(bucketKey)
            };

            // Persist the catalog entry before creating the indexes so a crash in between is recovered
            // by InitializeCore on the next start.
            catalog.Buckets.Add(bucket);
            await SaveCatalog(documentStore, cancellationToken);

            await EnsureBucketIndexes(documentStore, bucket, cancellationToken);

            RefreshActiveBuckets();
            logger.LogInformation("Created audit retention bucket {BucketKey} with collections {ProcessedMessageCollection} and {SagaSnapshotCollection}", bucket.Key, bucket.ProcessedMessageCollection, bucket.SagaSnapshotCollection);
            return bucket;
        }

        async Task CleanupExpiredBuckets(IDocumentStore documentStore, CancellationToken cancellationToken)
        {
            var now = UtcNow();
            var retention = configuration.AuditRetentionPeriod;

            var bucketsToClean = catalog.Buckets
                .Where(b => b.State == AuditRetentionBucketState.Retired
                    || (b.State == AuditRetentionBucketState.Active && b.End.Add(retention) <= now))
                .OrderBy(b => b.Start)
                .ToList();

            foreach (var bucket in bucketsToClean)
            {
                // 1. Make the bucket unavailable to reads through the catalog before touching anything else.
                if (bucket.State == AuditRetentionBucketState.Active)
                {
                    bucket.State = AuditRetentionBucketState.Retired;
                    await SaveCatalog(documentStore, cancellationToken);
                }

                // 2. Delete every dedicated static index.
                await DeleteIndexIfExists(documentStore, bucket.MessagesViewIndex, cancellationToken);
                await DeleteIndexIfExists(documentStore, bucket.MessagesViewFullTextIndex, cancellationToken);
                await DeleteIndexIfExists(documentStore, bucket.SagaDetailsIndex, cancellationToken);

                // 3. Delete the bucket collections.
                await DeleteCollection(documentStore, bucket.ProcessedMessageCollection, cancellationToken);
                await DeleteCollection(documentStore, bucket.SagaSnapshotCollection, cancellationToken);

                // 4. Remove the bucket from the catalog.
                catalog.Buckets.Remove(bucket);
                await SaveCatalog(documentStore, cancellationToken);

                logger.LogInformation("Cleaned up expired audit retention bucket {BucketKey}", bucket.Key);
            }

            RefreshActiveBuckets();
        }

        async Task EnsureBucketIndexes(IDocumentStore documentStore, AuditRetentionBucket bucket, CancellationToken cancellationToken)
        {
            var definitions = new List<IndexDefinition>();

            if (configuration.EnableFullTextSearch)
            {
                if (await documentStore.Maintenance.SendAsync(new GetIndexOperation(bucket.MessagesViewFullTextIndex), cancellationToken) == null)
                {
                    definitions.Add(AuditRetentionBucketIndexes.MessagesViewWithFullTextSearch(bucket));
                }
            }
            else if (await documentStore.Maintenance.SendAsync(new GetIndexOperation(bucket.MessagesViewIndex), cancellationToken) == null)
            {
                definitions.Add(AuditRetentionBucketIndexes.MessagesView(bucket));
            }

            if (await documentStore.Maintenance.SendAsync(new GetIndexOperation(bucket.SagaDetailsIndex), cancellationToken) == null)
            {
                definitions.Add(AuditRetentionBucketIndexes.SagaDetails(bucket));
            }

            if (definitions.Count > 0)
            {
                await documentStore.Maintenance.SendAsync(new PutIndexesOperation(definitions.ToArray()), cancellationToken);
            }
        }

        static async Task DeleteIndexIfExists(IDocumentStore documentStore, string indexName, CancellationToken cancellationToken)
        {
            if (await documentStore.Maintenance.SendAsync(new GetIndexOperation(indexName), cancellationToken) != null)
            {
                await documentStore.Maintenance.SendAsync(new DeleteIndexOperation(indexName), cancellationToken);
            }
        }

        static async Task DeleteCollection(IDocumentStore documentStore, string collectionName, CancellationToken cancellationToken)
        {
            var operation = await documentStore.Operations.SendAsync(new DeleteByQueryOperation(new IndexQuery
            {
                Query = $"from '{collectionName}'"
            }), token: cancellationToken);

            await operation.WaitForCompletionAsync(cancellationToken);
        }

        async Task<AuditRetentionBucketCatalog> LoadOrCreateCatalog(IDocumentStore documentStore, CancellationToken cancellationToken)
        {
            using var session = documentStore.OpenAsyncSession();
            var existing = await session.LoadAsync<AuditRetentionBucketCatalog>(AuditRetentionBucketCatalog.DocumentId, cancellationToken);
            if (existing != null)
            {
                var configuredDuration = System.Xml.XmlConvert.ToString(DatabaseConfiguration.AuditRetentionBucketDuration);
                if (!string.Equals(existing.BucketDuration, configuredDuration, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"The audit retention bucket duration stored in the catalog ({existing.BucketDuration}) does not match the configured duration ({configuredDuration}). The bucket duration cannot be changed for an existing database.");
                }

                return existing;
            }

            var catalog = new AuditRetentionBucketCatalog
            {
                Id = AuditRetentionBucketCatalog.DocumentId,
                BucketDuration = System.Xml.XmlConvert.ToString(DatabaseConfiguration.AuditRetentionBucketDuration)
            };
            await session.StoreAsync(catalog, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
            return catalog;
        }

        async Task SaveCatalog(IDocumentStore documentStore, CancellationToken cancellationToken)
        {
            using var session = documentStore.OpenAsyncSession();
            await session.StoreAsync(catalog, catalog.Id, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }

        void RefreshActiveBuckets()
        {
            activeBuckets = catalog.Buckets
                .Where(b => b.State == AuditRetentionBucketState.Active)
                .OrderBy(b => b.Start)
                .ToList();
        }

        DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

        AuditRetentionBucketCatalog catalog;
        AuditRetentionBucket currentBucket;
        IReadOnlyList<AuditRetentionBucket> activeBuckets = [];
        bool initialized;
        readonly SemaphoreSlim gate = new(1, 1);
    }
}
