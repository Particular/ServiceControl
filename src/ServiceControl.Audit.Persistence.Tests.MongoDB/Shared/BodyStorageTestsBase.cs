namespace ServiceControl.Audit.Persistence.Tests.MongoDB.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using global::MongoDB.Bson;
    using global::MongoDB.Driver;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using NUnit.Framework;
    using ServiceControl.Audit.Auditing;
    using ServiceControl.Audit.Auditing.BodyStorage;
    using ServiceControl.Audit.Persistence.MongoDB;
    using ServiceControl.Audit.Persistence.MongoDB.Collections;
    using ServiceControl.Audit.Persistence.UnitOfWork;
    using Infrastructure;

    /// <summary>
    /// Base class for body storage tests that can run against different MongoDB-compatible products.
    /// </summary>
    public abstract class BodyStorageTestsBase
    {
        protected IMongoTestEnvironment Environment { get; private set; }

        IHost host;
        IMongoDatabase database;
        string databaseName;

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

        [SetUp]
        public async Task SetUp()
        {
            databaseName = $"test_{Guid.NewGuid():N}";
            var connectionString = Environment.BuildConnectionString(databaseName);

            var persistenceSettings = new PersistenceSettings(TimeSpan.FromHours(1), true, 100000);
            persistenceSettings.PersisterSpecificSettings[MongoPersistenceConfiguration.ConnectionStringKey] = connectionString;

            var config = new MongoPersistenceConfiguration();
            var persistence = config.Create(persistenceSettings);

            var hostBuilder = Host.CreateApplicationBuilder();
            persistence.AddPersistence(hostBuilder.Services);

            host = hostBuilder.Build();
            await host.StartAsync().ConfigureAwait(false);

            var clientProvider = host.Services.GetRequiredService<IMongoClientProvider>();
            database = clientProvider.Database;
        }

        [TearDown]
        public async Task TearDown()
        {
            if (database != null)
            {
                await database.Client.DropDatabaseAsync(databaseName).ConfigureAwait(false);
            }

            if (host != null)
            {
                await host.StopAsync().ConfigureAwait(false);
                host.Dispose();
            }
        }

        [Test]
        public async Task Should_store_and_retrieve_text_body()
        {
            var factory = host.Services.GetRequiredService<IAuditIngestionUnitOfWorkFactory>();
            var bodyStorage = host.Services.GetRequiredService<IBodyStorage>();

            var messageId = "text-body-test";
            var bodyContent = "{ \"message\": \"Hello, World!\" }";
            var message = CreateProcessedMessage(messageId, "application/json");

            // Ingest message with body via unit of work
            await IngestMessage(factory, message, Encoding.UTF8.GetBytes(bodyContent)).ConfigureAwait(false);

            // Retrieve body via IBodyStorage
            var result = await bodyStorage.TryFetch(messageId, CancellationToken.None).ConfigureAwait(false);

            Assert.Multiple(() =>
            {
                Assert.That(result.HasResult, Is.True, "Should find stored body");
                Assert.That(result.ContentType, Is.EqualTo("application/json"), "Content type should match");
                Assert.That(result.BodySize, Is.EqualTo(Encoding.UTF8.GetByteCount(bodyContent)), "Body size should match");
            });

            using var reader = new StreamReader(result.Stream);
            var retrievedContent = await reader.ReadToEndAsync().ConfigureAwait(false);
            Assert.That(retrievedContent, Is.EqualTo(bodyContent), "Body content should match");
        }

        [Test]
        public async Task Should_store_and_retrieve_binary_body()
        {
            var factory = host.Services.GetRequiredService<IAuditIngestionUnitOfWorkFactory>();
            var bodyStorage = host.Services.GetRequiredService<IBodyStorage>();

            var messageId = "binary-body-test";
            // Invalid UTF-8 sequence - will be stored as binary, not text
            var binaryContent = new byte[] { 0x00, 0x01, 0x02, 0xFF, 0xFE, 0xFD };
            var message = CreateProcessedMessage(messageId, "application/octet-stream");

            // Ingest message with binary body
            await IngestMessage(factory, message, binaryContent).ConfigureAwait(false);

            // Binary bodies should be retrievable
            var result = await bodyStorage.TryFetch(messageId, CancellationToken.None).ConfigureAwait(false);

            Assert.Multiple(() =>
            {
                Assert.That(result.HasResult, Is.True, "Should find stored binary body");
                Assert.That(result.ContentType, Is.EqualTo("application/octet-stream"), "Content type should match");
                Assert.That(result.BodySize, Is.EqualTo(binaryContent.Length), "Body size should match");
            });

            // Verify content matches
            using var memoryStream = new MemoryStream();
            await result.Stream.CopyToAsync(memoryStream).ConfigureAwait(false);
            Assert.That(memoryStream.ToArray(), Is.EqualTo(binaryContent), "Binary content should match");

            // Verify it's stored in binaryBody field, not body field
            var collection = database.GetCollection<BsonDocument>(CollectionNames.ProcessedMessages);
            var doc = await collection.Find(Builders<BsonDocument>.Filter.Eq("_id", messageId)).FirstOrDefaultAsync().ConfigureAwait(false);
            Assert.That(doc, Is.Not.Null, "Message should be stored");
            Assert.That(doc.Contains("body") && doc["body"] != BsonNull.Value, Is.False, "Text body field should be null for binary content");
            Assert.That(doc.Contains("binaryBody") && doc["binaryBody"] != BsonNull.Value, Is.True, "Binary body field should have content");
        }

        [Test]
        public async Task Should_return_no_result_for_nonexistent_body()
        {
            var bodyStorage = host.Services.GetRequiredService<IBodyStorage>();

            var result = await bodyStorage.TryFetch("nonexistent-body-id", CancellationToken.None).ConfigureAwait(false);

            Assert.That(result.HasResult, Is.False, "Should not find nonexistent body");
        }

        [Test]
        public async Task Should_store_body_inline_in_processed_message()
        {
            var factory = host.Services.GetRequiredService<IAuditIngestionUnitOfWorkFactory>();

            var messageId = "inline-storage-test";
            var bodyContent = "{ \"test\": \"inline body storage\" }";
            var message = CreateProcessedMessage(messageId, "application/json");

            await IngestMessage(factory, message, Encoding.UTF8.GetBytes(bodyContent)).ConfigureAwait(false);

            // Verify body is stored inline in ProcessedMessages collection
            var collection = database.GetCollection<BsonDocument>(CollectionNames.ProcessedMessages);
            var doc = await collection.Find(Builders<BsonDocument>.Filter.Eq("_id", messageId)).FirstOrDefaultAsync().ConfigureAwait(false);

            Assert.That(doc, Is.Not.Null, "Message should be stored");
            Assert.That(doc.Contains("body"), Is.True, "Document should have body field");
            Assert.That(doc["body"].AsString, Is.EqualTo(bodyContent), "Body should be stored as plain UTF-8 text");
        }

        [Test]
        public async Task Should_not_store_body_when_body_storage_type_is_none()
        {
            // Arrange - Create a separate host with BodyStorageType.None
            var testDatabaseName = $"test_nobody_{Guid.NewGuid():N}";
            var connectionString = Environment.BuildConnectionString(testDatabaseName);

            var persistenceSettings = new PersistenceSettings(TimeSpan.FromHours(1), true, 100000);
            persistenceSettings.PersisterSpecificSettings[MongoPersistenceConfiguration.ConnectionStringKey] = connectionString;
            persistenceSettings.PersisterSpecificSettings[MongoPersistenceConfiguration.BodyStorageTypeKey] = "None";

            var config = new MongoPersistenceConfiguration();
            var persistence = config.Create(persistenceSettings);

            var hostBuilder = Host.CreateApplicationBuilder();
            persistence.AddPersistence(hostBuilder.Services);

            using var testHost = hostBuilder.Build();
            await testHost.StartAsync().ConfigureAwait(false);

            try
            {
                var clientProvider = testHost.Services.GetRequiredService<IMongoClientProvider>();
                var testDatabase = clientProvider.Database;
                var factory = testHost.Services.GetRequiredService<IAuditIngestionUnitOfWorkFactory>();
                var bodyStorage = testHost.Services.GetRequiredService<IBodyStorage>();

                var messageId = "none-storage-test";
                var bodyContent = "This body should NOT be stored";
                var message = CreateProcessedMessage(messageId, "text/plain");

                // Act - Ingest message with body
                await IngestMessage(factory, message, Encoding.UTF8.GetBytes(bodyContent)).ConfigureAwait(false);

                // Assert - TryFetch should return no result
                var result = await bodyStorage.TryFetch(messageId, CancellationToken.None).ConfigureAwait(false);
                Assert.That(result.HasResult, Is.False, "Body should not be retrievable when BodyStorageType is None");

                // Assert - Message should be stored but without body
                var collection = testDatabase.GetCollection<BsonDocument>(CollectionNames.ProcessedMessages);
                var doc = await collection.Find(Builders<BsonDocument>.Filter.Eq("_id", messageId)).FirstOrDefaultAsync().ConfigureAwait(false);
                Assert.That(doc, Is.Not.Null, "Message should be stored");
                Assert.That(doc.Contains("body") && doc["body"] != BsonNull.Value, Is.False, "Body field should be null when BodyStorageType is None");
            }
            finally
            {
                var client = new MongoClient(connectionString);
                await client.DropDatabaseAsync(testDatabaseName).ConfigureAwait(false);
                await testHost.StopAsync().ConfigureAwait(false);
            }
        }

        [Test]
        public async Task Should_not_store_body_when_body_exceeds_max_size()
        {
            // Arrange - Create a host with a small max body size
            var testDatabaseName = $"test_maxsize_{Guid.NewGuid():N}";
            var connectionString = Environment.BuildConnectionString(testDatabaseName);

            // Set max body size to 10 bytes
            var persistenceSettings = new PersistenceSettings(TimeSpan.FromHours(1), true, 10);
            persistenceSettings.PersisterSpecificSettings[MongoPersistenceConfiguration.ConnectionStringKey] = connectionString;

            var config = new MongoPersistenceConfiguration();
            var persistence = config.Create(persistenceSettings);

            var hostBuilder = Host.CreateApplicationBuilder();
            persistence.AddPersistence(hostBuilder.Services);

            using var testHost = hostBuilder.Build();
            await testHost.StartAsync().ConfigureAwait(false);

            try
            {
                var clientProvider = testHost.Services.GetRequiredService<IMongoClientProvider>();
                var testDatabase = clientProvider.Database;
                var factory = testHost.Services.GetRequiredService<IAuditIngestionUnitOfWorkFactory>();
                var bodyStorage = testHost.Services.GetRequiredService<IBodyStorage>();

                var messageId = "large-body-msg";
                var message = CreateProcessedMessage(messageId, "text/plain");
                var body = Encoding.UTF8.GetBytes("This body is larger than 10 bytes and should NOT be stored");

                // Act - Ingest a message with a body that exceeds max size
                await IngestMessage(factory, message, body).ConfigureAwait(false);

                // Assert - Message should be stored, but body should NOT (too large)
                var collection = testDatabase.GetCollection<BsonDocument>(CollectionNames.ProcessedMessages);
                var doc = await collection.Find(Builders<BsonDocument>.Filter.Eq("_id", messageId)).FirstOrDefaultAsync().ConfigureAwait(false);

                Assert.That(doc, Is.Not.Null, "Message should be stored");
                Assert.That(doc.Contains("body") && doc["body"] != BsonNull.Value, Is.False, "Body should NOT be stored when it exceeds max size");

                // Assert - TryFetch should return no result
                var result = await bodyStorage.TryFetch(messageId, CancellationToken.None).ConfigureAwait(false);
                Assert.That(result.HasResult, Is.False, "Body should not be retrievable when it exceeds max size");
            }
            finally
            {
                var client = new MongoClient(connectionString);
                await client.DropDatabaseAsync(testDatabaseName).ConfigureAwait(false);
                await testHost.StopAsync().ConfigureAwait(false);
            }
        }

        static async Task IngestMessage(IAuditIngestionUnitOfWorkFactory factory, ProcessedMessage message, byte[] body)
        {
            var unitOfWork = await factory.StartNew(10, CancellationToken.None).ConfigureAwait(false);
            try
            {
                await unitOfWork.RecordProcessedMessage(message, body, CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                await unitOfWork.DisposeAsync().ConfigureAwait(false);
            }
        }

        static ProcessedMessage CreateProcessedMessage(string messageId, string contentType = "text/plain")
        {
            var headers = new Dictionary<string, string>
            {
                ["NServiceBus.MessageId"] = messageId,
                ["NServiceBus.ContentType"] = contentType,
                ["NServiceBus.ProcessingStarted"] = DateTime.UtcNow.AddSeconds(-1).ToString("O"),
                ["NServiceBus.ProcessingEnded"] = DateTime.UtcNow.ToString("O"),
                ["$.diagnostics.originating.hostid"] = Guid.NewGuid().ToString(),
                ["NServiceBus.ProcessingEndpoint"] = "TestEndpoint"
            };

            var metadata = new Dictionary<string, object>
            {
                ["MessageId"] = messageId,
                ["MessageType"] = "TestMessage",
                ["TimeSent"] = DateTime.UtcNow,
                ["IsSystemMessage"] = false
            };

            return new ProcessedMessage(headers, metadata) { Id = messageId };
        }
    }
}
