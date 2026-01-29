namespace ServiceControl.Audit.Persistence.MongoDB
{
    using System.Threading;
    using System.Threading.Tasks;
    using global::MongoDB.Bson;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Manages the lifecycle of the MongoDB persistence layer.
    /// Handles initialization, connectivity verification, and shutdown.
    /// </summary>
    class MongoPersistenceLifecycle(
        MongoClientProvider clientProvider,
        MongoSettings settings,
        ILogger<MongoPersistenceLifecycle> logger) : IMongoPersistenceLifecycle
    {
        readonly MongoClientProvider clientProvider = clientProvider;
        readonly MongoSettings settings = settings;
        readonly ILogger<MongoPersistenceLifecycle> logger = logger;

        public async Task Initialize(CancellationToken cancellationToken = default)
        {
            logger.LogInformation("Initializing MongoDB persistence for database '{DatabaseName}'", settings.DatabaseName);

            // Initialize the client and detect product capabilities
            await clientProvider.InitializeAsync(cancellationToken).ConfigureAwait(false);

            // Verify connectivity with a ping
            await VerifyConnectivity(cancellationToken).ConfigureAwait(false);

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
            // Perform a ping command to verify connectivity
            var command = new BsonDocument("ping", 1);
            _ = await clientProvider.Database.RunCommandAsync<BsonDocument>(command, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }
}
