namespace ServiceControl.Audit.Persistence.MongoDB.Indexes
{
    using System.Threading;
    using System.Threading.Tasks;
    using Collections;
    using Documents;
    using global::MongoDB.Driver;
    using Microsoft.Extensions.Logging;

    class IndexInitializer(IMongoClientProvider clientProvider, ILogger<IndexInitializer> logger)
    {
        public async Task CreateIndexes(CancellationToken cancellationToken = default)
        {
            var database = clientProvider.Database;

            await CreateCollectionIndexes(
                database.GetCollection<ProcessedMessageDocument>(CollectionNames.ProcessedMessages),
                IndexDefinitions.ProcessedMessages,
                cancellationToken).ConfigureAwait(false);

            await CreateCollectionIndexes(
                database.GetCollection<SagaSnapshotDocument>(CollectionNames.SagaSnapshots),
                IndexDefinitions.SagaSnapshots,
                cancellationToken).ConfigureAwait(false);

            await CreateCollectionIndexes(
                database.GetCollection<KnownEndpointDocument>(CollectionNames.KnownEndpoints),
                IndexDefinitions.KnownEndpoints,
                cancellationToken).ConfigureAwait(false);

            // FailedAuditImports has no additional indexes - queries are by _id only
        }

        async Task CreateCollectionIndexes<T>(
            IMongoCollection<T> collection,
            CreateIndexModel<T>[] indexes,
            CancellationToken cancellationToken)
        {
            if (indexes.Length == 0)
            {
                return;
            }

            logger.LogInformation(
                "Ensuring {IndexCount} indexes on collection '{CollectionName}'",
                indexes.Length,
                collection.CollectionNamespace.CollectionName);

            _ = await collection.Indexes
                .CreateManyAsync(indexes, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
