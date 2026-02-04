namespace ServiceControl.Audit.Persistence.MongoDB
{
    using System.Threading;
    using System.Threading.Tasks;
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
        ILogger<MongoPersistenceLifecycle> logger) : IMongoPersistenceLifecycle
    {
        static bool serializersRegistered;
        static readonly object serializerLock = new();

        readonly MongoClientProvider clientProvider = clientProvider;
        readonly MongoSettings settings = settings;
        readonly IndexInitializer indexInitializer = indexInitializer;
        readonly ILogger<MongoPersistenceLifecycle> logger = logger;

        public async Task Initialize(CancellationToken cancellationToken = default)
        {
            RegisterSerializers();
            logger.LogInformation("Initializing MongoDB persistence for database '{DatabaseName}'", settings.DatabaseName);
            logger.LogInformation("MongoDB settings: AuditRetentionPeriod={AuditRetentionPeriod}, BodyStorageType={BodyStorageType}, BodyStoragePath={BodyStoragePath}, EnableFullTextSearchOnBodies={EnableFullTextSearchOnBodies}, MaxBodySizeToStore={MaxBodySizeToStore}",
                settings.AuditRetentionPeriod,
                settings.BodyStorageType,
                settings.BodyStoragePath ?? "(not set)",
                settings.EnableFullTextSearchOnBodies,
                settings.MaxBodySizeToStore);

            // Initialize the client and detect product capabilities
            await clientProvider.InitializeAsync(cancellationToken).ConfigureAwait(false);

            // Verify connectivity with a ping
            await VerifyConnectivity(cancellationToken).ConfigureAwait(false);

            // Create indexes
            await indexInitializer.CreateIndexes(cancellationToken).ConfigureAwait(false);

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
