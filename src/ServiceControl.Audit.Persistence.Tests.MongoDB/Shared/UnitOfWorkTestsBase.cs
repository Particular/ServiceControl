namespace ServiceControl.Audit.Persistence.Tests.MongoDB.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using global::MongoDB.Bson;
    using global::MongoDB.Driver;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using NUnit.Framework;
    using ServiceControl.Audit.Auditing;
    using ServiceControl.Audit.Persistence.MongoDB;
    using ServiceControl.Audit.Persistence.MongoDB.Collections;
    using ServiceControl.Audit.Persistence.Monitoring;
    using ServiceControl.Audit.Persistence.UnitOfWork;
    using ServiceControl.SagaAudit;
    using Infrastructure;

    /// <summary>
    /// Base class for UnitOfWork tests that can run against different MongoDB-compatible products.
    /// Subclasses provide the specific test environment.
    /// </summary>
    public abstract class UnitOfWorkTestsBase
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
        public async Task Should_insert_multiple_document_types_in_single_unit_of_work()
        {
            var factory = host.Services.GetRequiredService<IAuditIngestionUnitOfWorkFactory>();

            var message = CreateProcessedMessage("multi-type-msg");
            var body = System.Text.Encoding.UTF8.GetBytes("Test body");

            var endpoint = new KnownEndpoint
            {
                Name = "MultiTypeEndpoint",
                HostId = Guid.NewGuid(),
                Host = "localhost",
                LastSeen = DateTime.UtcNow
            };

            var sagaSnapshot = new SagaSnapshot
            {
                SagaId = Guid.NewGuid(),
                SagaType = "MultiTypeSaga",
                StartTime = DateTime.UtcNow.AddMinutes(-1),
                FinishTime = DateTime.UtcNow,
                Status = SagaStateChangeStatus.Updated,
                StateAfterChange = "{ }",
                Endpoint = "MultiTypeEndpoint",
                ProcessedAt = DateTime.UtcNow,
                InitiatingMessage = new InitiatingMessage
                {
                    MessageId = "multi-type-msg",
                    MessageType = "TestMessage",
                    IsSagaTimeoutMessage = false,
                    OriginatingEndpoint = "Sender",
                    OriginatingMachine = "localhost",
                    TimeSent = DateTime.UtcNow.AddMinutes(-1),
                    Intent = "Send"
                },
                OutgoingMessages = []
            };

            // Record all three document types in a single unit of work
            var unitOfWork = await factory.StartNew(10, CancellationToken.None).ConfigureAwait(false);
            try
            {
                await unitOfWork.RecordProcessedMessage(message, body, CancellationToken.None).ConfigureAwait(false);
                await unitOfWork.RecordKnownEndpoint(endpoint, CancellationToken.None).ConfigureAwait(false);
                await unitOfWork.RecordSagaSnapshot(sagaSnapshot, CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                await unitOfWork.DisposeAsync().ConfigureAwait(false);
            }

            // Verify all three collections have the expected documents
            var messagesCollection = database.GetCollection<BsonDocument>(CollectionNames.ProcessedMessages);
            var endpointsCollection = database.GetCollection<BsonDocument>(CollectionNames.KnownEndpoints);
            var sagasCollection = database.GetCollection<BsonDocument>(CollectionNames.SagaSnapshots);

            var messageCount = await messagesCollection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty).ConfigureAwait(false);
            var endpointCount = await endpointsCollection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty).ConfigureAwait(false);
            var sagaCount = await sagasCollection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty).ConfigureAwait(false);

            Assert.Multiple(() =>
            {
                Assert.That(messageCount, Is.EqualTo(1), "ProcessedMessage should be persisted");
                Assert.That(endpointCount, Is.EqualTo(1), "KnownEndpoint should be persisted");
                Assert.That(sagaCount, Is.EqualTo(1), "SagaSnapshot should be persisted");
            });
        }

        static ProcessedMessage CreateProcessedMessage(string messageId)
        {
            var headers = new Dictionary<string, string>
            {
                ["NServiceBus.MessageId"] = messageId,
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

            return new ProcessedMessage(headers, metadata);
        }
    }
}
