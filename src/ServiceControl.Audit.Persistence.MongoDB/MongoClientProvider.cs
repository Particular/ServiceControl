#nullable enable

namespace ServiceControl.Audit.Persistence.MongoDB
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using global::MongoDB.Driver;
    using ProductCapabilities;

    /// <summary>
    /// Provides access to the MongoDB client and database.
    /// Manages the client lifecycle and product capability detection.
    /// </summary>
    class MongoClientProvider(MongoSettings settings) : IMongoClientProvider, IAsyncDisposable
    {
        IMongoClient? client;
        bool initialized;

        public IMongoClient Client
        {
            get
            {
                EnsureInitialized();
                return client!;
            }
        }

        public IMongoDatabase Database { get; private set; } = null!;

        public IMongoProductCapabilities ProductCapabilities { get; private set; } = null!;

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (initialized)
            {
                return;
            }

            var mongoUrl = MongoUrl.Create(settings.ConnectionString);
            var clientSettings = MongoClientSettings.FromUrl(mongoUrl);

            // Configure client settings
            clientSettings.ApplicationName = "ServiceControl.Audit";

            client = new MongoClient(clientSettings);
            Database = client.GetDatabase(settings.DatabaseName);

            // Detect product capabilities
            ProductCapabilities = await MongoProductDetector.DetectAsync(client, settings.ConnectionString, cancellationToken).ConfigureAwait(false);

            initialized = true;
        }

        public ValueTask DisposeAsync()
        {
            // MongoClient doesn't need explicit disposal in MongoDB.Driver 3.x
            // but we implement IAsyncDisposable for future-proofing
            client = null;
            Database = null!;
            ProductCapabilities = null!;
            initialized = false;

            return ValueTask.CompletedTask;
        }

        void EnsureInitialized()
        {
            if (!initialized)
            {
                throw new InvalidOperationException("MongoClientProvider has not been initialized. Call InitializeAsync first.");
            }
        }
    }
}
