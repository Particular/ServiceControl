namespace ServiceControl.Audit.Persistence.Tests.MongoDB.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using global::MongoDB.Bson;
    using global::MongoDB.Driver;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence.MongoDB;
    using Infrastructure;

    /// <summary>
    /// Base class for lifecycle tests that can run against different MongoDB-compatible products.
    /// Subclasses provide the specific test environment.
    /// </summary>
    public abstract class LifecycleTestsBase
    {
        protected IMongoTestEnvironment Environment { get; private set; }

        /// <summary>
        /// Creates the test environment for this test fixture.
        /// </summary>
        protected abstract IMongoTestEnvironment CreateEnvironment();

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            Environment = CreateEnvironment();
            await Environment.Initialize().ConfigureAwait(false);
        }

        [OneTimeTearDown]
        public async Task OneTimeTearDown()
        {
            if (Environment != null)
            {
                await Environment.Cleanup().ConfigureAwait(false);
            }
        }

        [Test]
        public async Task Should_initialize_and_connect()
        {
            var connectionString = Environment.BuildConnectionString($"lifecycle_test_{Guid.NewGuid():N}");

            var persistenceSettings = new PersistenceSettings(TimeSpan.FromHours(1), true, 100000);
            persistenceSettings.PersisterSpecificSettings[MongoPersistenceConfiguration.ConnectionStringKey] = connectionString;

            var config = new MongoPersistenceConfiguration();
            var persistence = config.Create(persistenceSettings);

            var hostBuilder = Host.CreateApplicationBuilder();
            persistence.AddPersistence(hostBuilder.Services);

            using var host = hostBuilder.Build();
            await host.StartAsync().ConfigureAwait(false);

            try
            {
                var clientProvider = host.Services.GetRequiredService<IMongoClientProvider>();

                Assert.Multiple(() =>
                {
                    Assert.That(clientProvider.Client, Is.Not.Null);
                    Assert.That(clientProvider.Database, Is.Not.Null);
                    Assert.That(clientProvider.ProductCapabilities, Is.Not.Null);
                });
            }
            finally
            {
                await host.StopAsync().ConfigureAwait(false);
            }
        }

        [Test]
        public async Task Should_detect_product_capabilities()
        {
            var connectionString = Environment.BuildConnectionString($"capability_test_{Guid.NewGuid():N}");

            var persistenceSettings = new PersistenceSettings(TimeSpan.FromHours(1), true, 100000);
            persistenceSettings.PersisterSpecificSettings[MongoPersistenceConfiguration.ConnectionStringKey] = connectionString;

            var config = new MongoPersistenceConfiguration();
            var persistence = config.Create(persistenceSettings);

            var hostBuilder = Host.CreateApplicationBuilder();
            persistence.AddPersistence(hostBuilder.Services);

            using var host = hostBuilder.Build();
            await host.StartAsync().ConfigureAwait(false);

            try
            {
                var clientProvider = host.Services.GetRequiredService<IMongoClientProvider>();
                var capabilities = clientProvider.ProductCapabilities;
                var expected = Environment.GetExpectedCapabilities();

                Assert.Multiple(() =>
                {
                    Assert.That(capabilities.ProductName, Is.EqualTo(expected.ProductName),
                        $"Expected product '{expected.ProductName}' but got '{capabilities.ProductName}'");
                    Assert.That(capabilities.SupportsMultiCollectionBulkWrite, Is.EqualTo(expected.SupportsMultiCollectionBulkWrite),
                        $"SupportsMultiCollectionBulkWrite mismatch for {expected.ProductName}");
                    Assert.That(capabilities.SupportsTextIndexes, Is.EqualTo(expected.SupportsTextIndexes),
                        $"SupportsTextIndexes mismatch for {expected.ProductName}");
                    Assert.That(capabilities.SupportsTtlIndexes, Is.EqualTo(expected.SupportsTtlIndexes),
                        $"SupportsTtlIndexes mismatch for {expected.ProductName}");
                    Assert.That(capabilities.MaxDocumentSizeBytes, Is.EqualTo(expected.MaxDocumentSizeBytes),
                        $"MaxDocumentSizeBytes mismatch for {expected.ProductName}");
                });
            }
            finally
            {
                await host.StopAsync().ConfigureAwait(false);
            }
        }

        [Test]
        public async Task Should_create_processedMessages_indexes_on_startup()
        {
            var connectionString = Environment.BuildConnectionString($"index_test_{Guid.NewGuid():N}");

            var persistenceSettings = new PersistenceSettings(TimeSpan.FromHours(1), true, 100000);
            persistenceSettings.PersisterSpecificSettings[MongoPersistenceConfiguration.ConnectionStringKey] = connectionString;

            var config = new MongoPersistenceConfiguration();
            var persistence = config.Create(persistenceSettings);

            var hostBuilder = Host.CreateApplicationBuilder();
            persistence.AddPersistence(hostBuilder.Services);

            using var host = hostBuilder.Build();
            await host.StartAsync().ConfigureAwait(false);

            try
            {
                var clientProvider = host.Services.GetRequiredService<IMongoClientProvider>();
                var collection = clientProvider.Database.GetCollection<BsonDocument>("processedMessages");
                var indexes = await GetIndexNames(collection).ConfigureAwait(false);

                Assert.Multiple(() =>
                {
                    Assert.That(indexes, Does.Contain("processedAt_desc"), "Missing processedAt_desc index");
                    Assert.That(indexes, Does.Contain("timeSent_desc"), "Missing timeSent_desc index");
                    Assert.That(indexes, Does.Contain("endpoint_processedAt"), "Missing endpoint_processedAt index");
                    Assert.That(indexes, Does.Contain("conversationId"), "Missing conversationId index");
                    Assert.That(indexes, Does.Contain("ttl_expiry"), "Missing ttl_expiry index");
                });
            }
            finally
            {
                await host.StopAsync().ConfigureAwait(false);
            }
        }

        static async Task<List<string>> GetIndexNames(IMongoCollection<BsonDocument> collection)
        {
            var indexNames = new List<string>();
            using var cursor = await collection.Indexes.ListAsync().ConfigureAwait(false);

            while (await cursor.MoveNextAsync().ConfigureAwait(false))
            {
                foreach (var index in cursor.Current)
                {
                    if (index.TryGetValue("name", out var name))
                    {
                        indexNames.Add(name.AsString);
                    }
                }
            }

            return indexNames;
        }
    }
}
