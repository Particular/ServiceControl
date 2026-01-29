namespace ServiceControl.Audit.Persistence.Tests.MongoDB.Shared
{
    using System;
    using System.Threading.Tasks;
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
                    Assert.That(capabilities.SupportsGridFS, Is.EqualTo(expected.SupportsGridFS),
                        $"SupportsGridFS mismatch for {expected.ProductName}");
                    Assert.That(capabilities.SupportsTextIndexes, Is.EqualTo(expected.SupportsTextIndexes),
                        $"SupportsTextIndexes mismatch for {expected.ProductName}");
                    Assert.That(capabilities.SupportsTransactions, Is.EqualTo(expected.SupportsTransactions),
                        $"SupportsTransactions mismatch for {expected.ProductName}");
                    Assert.That(capabilities.SupportsTtlIndexes, Is.EqualTo(expected.SupportsTtlIndexes),
                        $"SupportsTtlIndexes mismatch for {expected.ProductName}");
                    Assert.That(capabilities.SupportsChangeStreams, Is.EqualTo(expected.SupportsChangeStreams),
                        $"SupportsChangeStreams mismatch for {expected.ProductName}");
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
        public async Task Should_use_correct_database_name()
        {
            var databaseName = $"custom_db_{Guid.NewGuid():N}";
            var connectionString = Environment.BuildConnectionString(databaseName);

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

                Assert.That(clientProvider.Database.DatabaseNamespace.DatabaseName, Is.EqualTo(databaseName));
            }
            finally
            {
                await host.StopAsync().ConfigureAwait(false);
            }
        }

        [Test]
        public async Task Should_default_database_name_to_audit()
        {
            // Connection string without database name
            var connectionString = Environment.GetConnectionString();

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

                Assert.That(clientProvider.Database.DatabaseNamespace.DatabaseName, Is.EqualTo("audit"));
            }
            finally
            {
                await host.StopAsync().ConfigureAwait(false);
            }
        }

        [Test]
        public async Task Should_start_and_stop_cleanly()
        {
            var connectionString = Environment.BuildConnectionString($"start_stop_test_{Guid.NewGuid():N}");

            var persistenceSettings = new PersistenceSettings(TimeSpan.FromHours(1), true, 100000);
            persistenceSettings.PersisterSpecificSettings[MongoPersistenceConfiguration.ConnectionStringKey] = connectionString;

            var config = new MongoPersistenceConfiguration();
            var persistence = config.Create(persistenceSettings);

            var hostBuilder = Host.CreateApplicationBuilder();
            persistence.AddPersistence(hostBuilder.Services);

            using var host = hostBuilder.Build();

            // Start
            await host.StartAsync().ConfigureAwait(false);

            var clientProvider = host.Services.GetRequiredService<IMongoClientProvider>();
            Assert.That(clientProvider.Client, Is.Not.Null);

            // Stop
            await host.StopAsync().ConfigureAwait(false);

            // Host should be stopped without errors
            Assert.Pass("Host started and stopped cleanly");
        }
    }
}
