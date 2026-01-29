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
        readonly MongoSettings settings = settings;
        IMongoClient? client;
        IMongoDatabase? database;
        IMongoProductCapabilities? productCapabilities;
        bool initialized;

        public IMongoClient Client
        {
            get
            {
                EnsureInitialized();
                return client!;
            }
        }

        public IMongoDatabase Database
        {
            get
            {
                EnsureInitialized();
                return database!;
            }
        }

        public IMongoProductCapabilities ProductCapabilities
        {
            get
            {
                EnsureInitialized();
                return productCapabilities!;
            }
        }

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
            database = client.GetDatabase(settings.DatabaseName);

            // Detect product capabilities
            productCapabilities = await MongoProductDetector.DetectAsync(client, settings.ConnectionString, cancellationToken).ConfigureAwait(false);

            initialized = true;
        }

        public ValueTask DisposeAsync()
        {
            // MongoClient doesn't need explicit disposal in MongoDB.Driver 3.x
            // but we implement IAsyncDisposable for future-proofing
            client = null;
            database = null;
            productCapabilities = null;
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
