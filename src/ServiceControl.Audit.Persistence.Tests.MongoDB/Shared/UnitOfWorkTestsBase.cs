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
        public async Task Should_insert_processed_message()
        {
            var factory = host.Services.GetRequiredService<IAuditIngestionUnitOfWorkFactory>();

            var message = CreateProcessedMessage("msg-1");
            var body = System.Text.Encoding.UTF8.GetBytes("Hello World");

            var unitOfWork = await factory.StartNew(10, CancellationToken.None).ConfigureAwait(false);
            try
            {
                await unitOfWork.RecordProcessedMessage(message, body, CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                await unitOfWork.DisposeAsync().ConfigureAwait(false);
            }

            var collection = database.GetCollection<BsonDocument>(CollectionNames.ProcessedMessages);
            var result = await collection.Find(Builders<BsonDocument>.Filter.Eq("_id", message.Id)).FirstOrDefaultAsync().ConfigureAwait(false);

            Assert.That(result, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(result["uniqueMessageId"].AsString, Is.EqualTo(message.UniqueMessageId));
                Assert.That(result["body"].AsString, Is.Not.Empty);
                Assert.That(result.Contains("expiresAt"), Is.True);
            });
        }

        [Test]
        public async Task Should_insert_batch_of_processed_messages()
        {
            var factory = host.Services.GetRequiredService<IAuditIngestionUnitOfWorkFactory>();

            var unitOfWork = await factory.StartNew(10, CancellationToken.None).ConfigureAwait(false);
            try
            {
                for (int i = 0; i < 5; i++)
                {
                    var message = CreateProcessedMessage($"batch-msg-{i}");
                    await unitOfWork.RecordProcessedMessage(message, default, CancellationToken.None).ConfigureAwait(false);
                }
            }
            finally
            {
                await unitOfWork.DisposeAsync().ConfigureAwait(false);
            }

            var collection = database.GetCollection<BsonDocument>(CollectionNames.ProcessedMessages);
            var count = await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty).ConfigureAwait(false);

            Assert.That(count, Is.EqualTo(5));
        }

        [Test]
        public async Task Should_handle_duplicate_processed_messages()
        {
            var factory = host.Services.GetRequiredService<IAuditIngestionUnitOfWorkFactory>();

            var message = CreateProcessedMessage("dup-msg");

            // Insert first time
            var unitOfWork1 = await factory.StartNew(10, CancellationToken.None).ConfigureAwait(false);
            try
            {
                await unitOfWork1.RecordProcessedMessage(message, default, CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                await unitOfWork1.DisposeAsync().ConfigureAwait(false);
            }

            // Insert same message again (upsert should succeed)
            var unitOfWork2 = await factory.StartNew(10, CancellationToken.None).ConfigureAwait(false);
            try
            {
                await unitOfWork2.RecordProcessedMessage(message, default, CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                await unitOfWork2.DisposeAsync().ConfigureAwait(false);
            }

            var collection = database.GetCollection<BsonDocument>(CollectionNames.ProcessedMessages);
            var count = await collection.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("_id", message.Id)).ConfigureAwait(false);

            Assert.That(count, Is.EqualTo(1), "Duplicate message should result in upsert, not duplicate");
        }

        [Test]
        public async Task Should_insert_known_endpoint()
        {
            var factory = host.Services.GetRequiredService<IAuditIngestionUnitOfWorkFactory>();

            var endpoint = new KnownEndpoint
            {
                Name = "TestEndpoint",
                HostId = Guid.NewGuid(),
                Host = "localhost",
                LastSeen = DateTime.UtcNow
            };

            var unitOfWork = await factory.StartNew(10, CancellationToken.None).ConfigureAwait(false);
            try
            {
                await unitOfWork.RecordKnownEndpoint(endpoint, CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                await unitOfWork.DisposeAsync().ConfigureAwait(false);
            }

            var collection = database.GetCollection<BsonDocument>(CollectionNames.KnownEndpoints);
            var documentId = KnownEndpoint.MakeDocumentId(endpoint.Name, endpoint.HostId);
            var result = await collection.Find(Builders<BsonDocument>.Filter.Eq("_id", documentId)).FirstOrDefaultAsync().ConfigureAwait(false);

            Assert.That(result, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(result["name"].AsString, Is.EqualTo(endpoint.Name));
                Assert.That(result["host"].AsString, Is.EqualTo(endpoint.Host));
                Assert.That(result.Contains("expiresAt"), Is.True);
            });
        }

        [Test]
        public async Task Should_update_known_endpoint_on_duplicate()
        {
            var factory = host.Services.GetRequiredService<IAuditIngestionUnitOfWorkFactory>();

            var hostId = Guid.NewGuid();
            var endpoint1 = new KnownEndpoint
            {
                Name = "TestEndpoint",
                HostId = hostId,
                Host = "host1",
                LastSeen = DateTime.UtcNow.AddHours(-1)
            };

            var unitOfWork1 = await factory.StartNew(10, CancellationToken.None).ConfigureAwait(false);
            try
            {
                await unitOfWork1.RecordKnownEndpoint(endpoint1, CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                await unitOfWork1.DisposeAsync().ConfigureAwait(false);
            }

            var endpoint2 = new KnownEndpoint
            {
                Name = "TestEndpoint",
                HostId = hostId,
                Host = "host2", // Different host
                LastSeen = DateTime.UtcNow
            };

            var unitOfWork2 = await factory.StartNew(10, CancellationToken.None).ConfigureAwait(false);
            try
            {
                await unitOfWork2.RecordKnownEndpoint(endpoint2, CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                await unitOfWork2.DisposeAsync().ConfigureAwait(false);
            }

            var collection = database.GetCollection<BsonDocument>(CollectionNames.KnownEndpoints);
            var documentId = KnownEndpoint.MakeDocumentId(endpoint1.Name, endpoint1.HostId);
            var result = await collection.Find(Builders<BsonDocument>.Filter.Eq("_id", documentId)).FirstOrDefaultAsync().ConfigureAwait(false);

            Assert.That(result, Is.Not.Null);
            Assert.That(result["host"].AsString, Is.EqualTo("host2"), "Should have updated to latest host value");
        }

        [Test]
        public async Task Should_insert_saga_snapshot()
        {
            var factory = host.Services.GetRequiredService<IAuditIngestionUnitOfWorkFactory>();

            var sagaSnapshot = new SagaSnapshot
            {
                SagaId = Guid.NewGuid(),
                SagaType = "TestSaga",
                StartTime = DateTime.UtcNow.AddMinutes(-5),
                FinishTime = DateTime.UtcNow,
                Status = SagaStateChangeStatus.Updated,
                StateAfterChange = "{ \"State\": \"Active\" }",
                Endpoint = "TestEndpoint",
                ProcessedAt = DateTime.UtcNow,
                InitiatingMessage = new InitiatingMessage
                {
                    MessageId = "init-msg-1",
                    MessageType = "TestMessage",
                    IsSagaTimeoutMessage = false,
                    OriginatingEndpoint = "Sender",
                    OriginatingMachine = "localhost",
                    TimeSent = DateTime.UtcNow.AddMinutes(-5),
                    Intent = "Send"
                },
                OutgoingMessages =
                [
                    new ResultingMessage
                    {
                        MessageId = "out-msg-1",
                        MessageType = "ResponseMessage",
                        Destination = "Receiver",
                        TimeSent = DateTime.UtcNow,
                        Intent = "Reply"
                    }
                ]
            };

            var unitOfWork = await factory.StartNew(10, CancellationToken.None).ConfigureAwait(false);
            try
            {
                await unitOfWork.RecordSagaSnapshot(sagaSnapshot, CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                await unitOfWork.DisposeAsync().ConfigureAwait(false);
            }

            var collection = database.GetCollection<BsonDocument>(CollectionNames.SagaSnapshots);
            var count = await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty).ConfigureAwait(false);

            Assert.That(count, Is.EqualTo(1));

            var result = await collection.Find(FilterDefinition<BsonDocument>.Empty).FirstOrDefaultAsync().ConfigureAwait(false);
            Assert.Multiple(() =>
            {
                Assert.That(result["sagaType"].AsString, Is.EqualTo("TestSaga"));
                Assert.That(result["endpoint"].AsString, Is.EqualTo("TestEndpoint"));
                Assert.That(result.Contains("expiresAt"), Is.True);
            });
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

        [Test]
        public async Task Should_set_expiration_based_on_retention_period()
        {
            var factory = host.Services.GetRequiredService<IAuditIngestionUnitOfWorkFactory>();

            var message = CreateProcessedMessage("expiry-test");
            var beforeInsert = DateTime.UtcNow;

            var unitOfWork = await factory.StartNew(10, CancellationToken.None).ConfigureAwait(false);
            try
            {
                await unitOfWork.RecordProcessedMessage(message, default, CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                await unitOfWork.DisposeAsync().ConfigureAwait(false);
            }

            var collection = database.GetCollection<BsonDocument>(CollectionNames.ProcessedMessages);
            var result = await collection.Find(Builders<BsonDocument>.Filter.Eq("_id", message.Id)).FirstOrDefaultAsync().ConfigureAwait(false);

            var expiresAt = result["expiresAt"].ToUniversalTime();
            var expectedExpiry = beforeInsert.AddHours(1); // Test uses 1 hour retention

            Assert.That(expiresAt, Is.GreaterThan(expectedExpiry.AddMinutes(-1)));
            Assert.That(expiresAt, Is.LessThan(expectedExpiry.AddMinutes(1)));
        }

        [Test]
        public void CanIngestMore_should_return_true()
        {
            var factory = host.Services.GetRequiredService<IAuditIngestionUnitOfWorkFactory>();

            // TODO: Stage 7 will implement proper storage monitoring
            // For now, it always returns true
            Assert.That(factory.CanIngestMore(), Is.True);
        }

        [Test]
        public async Task Should_handle_large_batch_with_multiple_document_types()
        {
            var factory = host.Services.GetRequiredService<IAuditIngestionUnitOfWorkFactory>();

            const int messageCount = 50;
            const int endpointCount = 10;
            const int sagaCount = 20;

            var unitOfWork = await factory.StartNew(messageCount + endpointCount + sagaCount, CancellationToken.None).ConfigureAwait(false);
            try
            {
                // Add many processed messages
                for (int i = 0; i < messageCount; i++)
                {
                    var message = CreateProcessedMessage($"large-batch-msg-{i}");
                    await unitOfWork.RecordProcessedMessage(message, default, CancellationToken.None).ConfigureAwait(false);
                }

                // Add many known endpoints
                for (int i = 0; i < endpointCount; i++)
                {
                    var endpoint = new KnownEndpoint
                    {
                        Name = $"LargeBatchEndpoint{i}",
                        HostId = Guid.NewGuid(),
                        Host = $"host{i}",
                        LastSeen = DateTime.UtcNow
                    };
                    await unitOfWork.RecordKnownEndpoint(endpoint, CancellationToken.None).ConfigureAwait(false);
                }

                // Add many saga snapshots
                for (int i = 0; i < sagaCount; i++)
                {
                    var saga = new SagaSnapshot
                    {
                        SagaId = Guid.NewGuid(),
                        SagaType = $"LargeBatchSaga{i}",
                        StartTime = DateTime.UtcNow.AddMinutes(-1),
                        FinishTime = DateTime.UtcNow,
                        Status = SagaStateChangeStatus.Updated,
                        StateAfterChange = "{ }",
                        Endpoint = $"LargeBatchEndpoint{i % endpointCount}",
                        ProcessedAt = DateTime.UtcNow,
                        InitiatingMessage = new InitiatingMessage
                        {
                            MessageId = $"large-batch-init-{i}",
                            MessageType = "TestMessage",
                            IsSagaTimeoutMessage = false,
                            OriginatingEndpoint = "Sender",
                            OriginatingMachine = "localhost",
                            TimeSent = DateTime.UtcNow.AddMinutes(-1),
                            Intent = "Send"
                        },
                        OutgoingMessages = []
                    };
                    await unitOfWork.RecordSagaSnapshot(saga, CancellationToken.None).ConfigureAwait(false);
                }
            }
            finally
            {
                await unitOfWork.DisposeAsync().ConfigureAwait(false);
            }

            // Verify all documents were persisted
            var messagesCollection = database.GetCollection<BsonDocument>(CollectionNames.ProcessedMessages);
            var endpointsCollection = database.GetCollection<BsonDocument>(CollectionNames.KnownEndpoints);
            var sagasCollection = database.GetCollection<BsonDocument>(CollectionNames.SagaSnapshots);

            var actualMessageCount = await messagesCollection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty).ConfigureAwait(false);
            var actualEndpointCount = await endpointsCollection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty).ConfigureAwait(false);
            var actualSagaCount = await sagasCollection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty).ConfigureAwait(false);

            Assert.Multiple(() =>
            {
                Assert.That(actualMessageCount, Is.EqualTo(messageCount), $"Expected {messageCount} processed messages");
                Assert.That(actualEndpointCount, Is.EqualTo(endpointCount), $"Expected {endpointCount} known endpoints");
                Assert.That(actualSagaCount, Is.EqualTo(sagaCount), $"Expected {sagaCount} saga snapshots");
            });
        }

        [Test]
        public async Task Should_handle_empty_commit()
        {
            var factory = host.Services.GetRequiredService<IAuditIngestionUnitOfWorkFactory>();

            // Create unit of work but don't add any documents
            var unitOfWork = await factory.StartNew(10, CancellationToken.None).ConfigureAwait(false);

            // Should not throw when committing empty batch
            await unitOfWork.DisposeAsync().ConfigureAwait(false);

            // Verify no documents were created
            var messagesCollection = database.GetCollection<BsonDocument>(CollectionNames.ProcessedMessages);
            var count = await messagesCollection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty).ConfigureAwait(false);

            Assert.That(count, Is.EqualTo(0));
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
