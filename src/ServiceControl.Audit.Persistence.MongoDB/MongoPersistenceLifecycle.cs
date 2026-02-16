namespace ServiceControl.Audit.Persistence.MongoDB
{
    using System.Threading;
    using System.Threading.Tasks;
    using Auditing.BodyStorage;
    using BodyStorage;
    using global::MongoDB.Bson;
    using global::MongoDB.Bson.Serialization;
    using global::MongoDB.Bson.Serialization.Serializers;
    using Indexes;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Manages the lifecycle of the MongoDB persistence layer.
    /// Handles initialization, connectivity verification, and shutdown.
    /// </summary>
    class MongoPersistenceLifecycle(
        MongoClientProvider clientProvider,
        MongoSettings settings,
        IndexInitializer indexInitializer,
        IBodyStorage bodyStorage,
        ILogger<MongoPersistenceLifecycle> logger) : IMongoPersistenceLifecycle
    {
        static bool serializersRegistered;
        static readonly object serializerLock = new();

        readonly MongoClientProvider clientProvider = clientProvider;
        readonly MongoSettings settings = settings;
        readonly IndexInitializer indexInitializer = indexInitializer;
        readonly IBodyStorage bodyStorage = bodyStorage;
        readonly ILogger<MongoPersistenceLifecycle> logger = logger;

        public async Task Initialize(CancellationToken cancellationToken = default)
        {
            RegisterSerializers();
            logger.LogInformation("Initializing MongoDB persistence for database '{DatabaseName}'", settings.DatabaseName);
            logger.LogInformation("MongoDB settings: AuditRetentionPeriod={AuditRetentionPeriod}, BodyStorageType={BodyStorageType}, EnableFullTextSearchOnBodies={EnableFullTextSearchOnBodies}, MaxBodySizeToStore={MaxBodySizeToStore}",
                settings.AuditRetentionPeriod,
                settings.BodyStorageType,
                settings.EnableFullTextSearchOnBodies,
                settings.MaxBodySizeToStore);
            logger.LogInformation("Body writer settings: BatchSize={BodyWriterBatchSize}, ParallelWriters={BodyWriterParallelWriters}, BatchTimeout={BodyWriterBatchTimeout}",
                settings.BodyWriterBatchSize,
                settings.BodyWriterParallelWriters,
                settings.BodyWriterBatchTimeout);

            if (settings.BodyStorageType is not BodyStorageType.Database and not BodyStorageType.None)
            {
                logger.LogWarning("Full text search on bodies is not available with {BodyStorageType} body storage. Full text search requires Database body storage.", settings.BodyStorageType);
            }

            // Initialize the client and detect product capabilities
            await clientProvider.InitializeAsync(cancellationToken).ConfigureAwait(false);

            // Verify connectivity with a ping
            await VerifyConnectivity(cancellationToken).ConfigureAwait(false);

            // Create indexes
            await indexInitializer.CreateIndexes(cancellationToken).ConfigureAwait(false);

            // Initialize blob storage container if using Azure Blob body storage
            if (bodyStorage is AzureBlobBodyStorage blobStorage)
            {
                await blobStorage.Initialize(cancellationToken).ConfigureAwait(false);
            }

            logger.LogInformation(
                "MongoDB persistence initialized. Product: {ProductName}, Database: {DatabaseName}",
                clientProvider.ProductCapabilities.ProductName,
                settings.DatabaseName);
        }

        public async Task Stop(CancellationToken cancellationToken = default)
        {
            logger.LogInformation("Stopping MongoDB persistence");
            await clientProvider.DisposeAsync().ConfigureAwait(false);
            logger.LogInformation("MongoDB persistence stopped");
        }

        async Task VerifyConnectivity(CancellationToken cancellationToken)
        {
            logger.LogInformation("Verifying MongoDB connectivity");
            var command = new BsonDocument("ping", 1);
            _ = await clientProvider.Database.RunCommandAsync<BsonDocument>(command, cancellationToken: cancellationToken).ConfigureAwait(false);
            logger.LogInformation("MongoDB connectivity verified");
        }

        static void RegisterSerializers()
        {
            if (serializersRegistered)
            {
                return;
            }

            lock (serializerLock)
            {
                if (serializersRegistered)
                {
                    return;
                }

                // Register Guid serializer with Standard representation to avoid "GuidRepresentation is Unspecified" errors
                try
                {
                    BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
                }
                catch (BsonSerializationException)
                {
                    // Serializer already registered by another component - this is fine
                }

                serializersRegistered = true;
            }
        }
    }
}
