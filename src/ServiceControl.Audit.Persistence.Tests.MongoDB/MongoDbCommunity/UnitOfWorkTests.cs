namespace ServiceControl.Audit.Persistence.Tests.MongoDB.MongoDbCommunity
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
    using Shared;

    /// <summary>
    /// UnitOfWork tests for MongoDB Community/Enterprise using Docker via Testcontainers.
    /// </summary>
    [TestFixture]
    [Category("MongoDbCommunity")]
    class UnitOfWorkTests : UnitOfWorkTestsBase
    {
        protected override IMongoTestEnvironment CreateEnvironment() => new MongoDbCommunityEnvironment();

        /// <summary>
        /// Verifies that MongoDB 8.0+ multi-collection bulk write feature works correctly.
        /// </summary>
        [Test]
        public async Task Should_use_multi_collection_bulk_write_for_multi_collection_operations()
        {
            // Arrange
            var databaseName = $"test_multi_bulk_{Guid.NewGuid():N}";
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
                var database = clientProvider.Database;
                var factory = host.Services.GetRequiredService<IAuditIngestionUnitOfWorkFactory>();

                // Verify capability is true for MongoDB 8.0+
                Assert.That(clientProvider.ProductCapabilities.SupportsMultiCollectionBulkWrite, Is.True,
                    "MongoDB 8.0+ should support multi-collection bulk write");

                // Act - Write to all three collections in a single unit of work
                var unitOfWork = await factory.StartNew(10, CancellationToken.None).ConfigureAwait(false);
                try
                {
                    await unitOfWork.RecordProcessedMessage(CreateProcessedMessage("multi-bulk-msg"), default, CancellationToken.None).ConfigureAwait(false);
                    await unitOfWork.RecordKnownEndpoint(CreateKnownEndpoint("MultiBulkEndpoint"), CancellationToken.None).ConfigureAwait(false);
                    await unitOfWork.RecordSagaSnapshot(CreateSagaSnapshot("MultiBulkSaga"), CancellationToken.None).ConfigureAwait(false);
                }
                finally
                {
                    await unitOfWork.DisposeAsync().ConfigureAwait(false);
                }

                // Assert - All collections should have documents
                var messagesCount = await database.GetCollection<BsonDocument>(CollectionNames.ProcessedMessages)
                    .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty).ConfigureAwait(false);
                var endpointsCount = await database.GetCollection<BsonDocument>(CollectionNames.KnownEndpoints)
                    .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty).ConfigureAwait(false);
                var sagasCount = await database.GetCollection<BsonDocument>(CollectionNames.SagaSnapshots)
                    .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty).ConfigureAwait(false);

                Assert.Multiple(() =>
                {
                    Assert.That(messagesCount, Is.EqualTo(1), "ProcessedMessages should be persisted via multi-collection bulk write");
                    Assert.That(endpointsCount, Is.EqualTo(1), "KnownEndpoints should be persisted via multi-collection bulk write");
                    Assert.That(sagasCount, Is.EqualTo(1), "SagaSnapshots should be persisted via multi-collection bulk write");
                });
            }
            finally
            {
                var client = new MongoClient(connectionString);
                await client.DropDatabaseAsync(databaseName).ConfigureAwait(false);
                await host.StopAsync().ConfigureAwait(false);
            }
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

        static KnownEndpoint CreateKnownEndpoint(string name) => new()
        {
            Name = name,
            HostId = Guid.NewGuid(),
            Host = "localhost",
            LastSeen = DateTime.UtcNow
        };

        static SagaSnapshot CreateSagaSnapshot(string sagaType) => new()
        {
            SagaId = Guid.NewGuid(),
            SagaType = sagaType,
            StartTime = DateTime.UtcNow.AddMinutes(-1),
            FinishTime = DateTime.UtcNow,
            Status = SagaStateChangeStatus.Updated,
            StateAfterChange = "{ }",
            Endpoint = "TestEndpoint",
            ProcessedAt = DateTime.UtcNow,
            InitiatingMessage = new InitiatingMessage
            {
                MessageId = $"{sagaType}-init-msg",
                MessageType = "TestMessage",
                IsSagaTimeoutMessage = false,
                OriginatingEndpoint = "Sender",
                OriginatingMachine = "localhost",
                TimeSent = DateTime.UtcNow.AddMinutes(-1),
                Intent = "Send"
            },
            OutgoingMessages = []
        };
    }
}
