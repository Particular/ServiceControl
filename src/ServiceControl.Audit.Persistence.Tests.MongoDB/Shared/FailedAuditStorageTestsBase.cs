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
    using Infrastructure;

    /// <summary>
    /// Base class for FailedAuditStorage tests that can run against different MongoDB-compatible products.
    /// </summary>
    public abstract class FailedAuditStorageTestsBase
    {
        protected IMongoTestEnvironment Environment { get; private set; }

        IHost host;
        IMongoDatabase database;
        string databaseName;
        IFailedAuditStorage failedAuditStorage;

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
            failedAuditStorage = host.Services.GetRequiredService<IFailedAuditStorage>();
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
        public async Task Should_save_failed_audit_import()
        {
            var failedImport = CreateFailedAuditImport("test-msg-1");

            await failedAuditStorage.SaveFailedAuditImport(failedImport).ConfigureAwait(false);

            var count = await failedAuditStorage.GetFailedAuditsCount().ConfigureAwait(false);
            Assert.That(count, Is.EqualTo(1));
        }

        [Test]
        public async Task Should_save_multiple_failed_audit_imports()
        {
            await failedAuditStorage.SaveFailedAuditImport(CreateFailedAuditImport("msg-1")).ConfigureAwait(false);
            await failedAuditStorage.SaveFailedAuditImport(CreateFailedAuditImport("msg-2")).ConfigureAwait(false);
            await failedAuditStorage.SaveFailedAuditImport(CreateFailedAuditImport("msg-3")).ConfigureAwait(false);

            var count = await failedAuditStorage.GetFailedAuditsCount().ConfigureAwait(false);
            Assert.That(count, Is.EqualTo(3));
        }

        [Test]
        public async Task Should_return_zero_when_no_failed_audits()
        {
            var count = await failedAuditStorage.GetFailedAuditsCount().ConfigureAwait(false);
            Assert.That(count, Is.EqualTo(0));
        }

        [Test]
        public async Task Should_process_failed_messages_and_delete_on_completion()
        {
            await failedAuditStorage.SaveFailedAuditImport(CreateFailedAuditImport("msg-1")).ConfigureAwait(false);
            await failedAuditStorage.SaveFailedAuditImport(CreateFailedAuditImport("msg-2")).ConfigureAwait(false);

            var processedCount = 0;
            var processedIds = new List<string>();

            await failedAuditStorage.ProcessFailedMessages(
                async (transportMessage, markComplete, token) =>
                {
                    processedIds.Add(transportMessage.Id);
                    processedCount++;
                    await markComplete(token).ConfigureAwait(false);
                },
                CancellationToken.None).ConfigureAwait(false);

            Assert.Multiple(() =>
            {
                Assert.That(processedCount, Is.EqualTo(2));
                Assert.That(processedIds, Has.Count.EqualTo(2));
            });

            var remainingCount = await failedAuditStorage.GetFailedAuditsCount().ConfigureAwait(false);
            Assert.That(remainingCount, Is.EqualTo(0), "All processed messages should be deleted");
        }

        [Test]
        public async Task Should_not_delete_failed_messages_when_not_marked_complete()
        {
            await failedAuditStorage.SaveFailedAuditImport(CreateFailedAuditImport("msg-1")).ConfigureAwait(false);
            await failedAuditStorage.SaveFailedAuditImport(CreateFailedAuditImport("msg-2")).ConfigureAwait(false);

            var processedCount = 0;

            await failedAuditStorage.ProcessFailedMessages(
                (transportMessage, markComplete, token) =>
                {
                    // Don't call markComplete - message should not be deleted
                    processedCount++;
                    return Task.CompletedTask;
                },
                CancellationToken.None).ConfigureAwait(false);

            Assert.That(processedCount, Is.EqualTo(2));

            var remainingCount = await failedAuditStorage.GetFailedAuditsCount().ConfigureAwait(false);
            Assert.That(remainingCount, Is.EqualTo(2), "Messages should remain when not marked complete");
        }

        [Test]
        public async Task Should_handle_empty_collection_when_processing()
        {
            var processedCount = 0;

            await failedAuditStorage.ProcessFailedMessages(
                (transportMessage, markComplete, token) =>
                {
                    processedCount++;
                    return Task.CompletedTask;
                },
                CancellationToken.None).ConfigureAwait(false);

            Assert.That(processedCount, Is.EqualTo(0));
        }

        [Test]
        public async Task Should_preserve_message_data_through_storage()
        {
            var originalMessage = CreateFailedAuditImport("preserve-test");
            originalMessage.Message.Headers["CustomHeader"] = "CustomValue";
            originalMessage.ExceptionInfo = "Test exception info";

            await failedAuditStorage.SaveFailedAuditImport(originalMessage).ConfigureAwait(false);

            FailedTransportMessage retrievedMessage = null;

            await failedAuditStorage.ProcessFailedMessages(
                (transportMessage, markComplete, token) =>
                {
                    retrievedMessage = transportMessage;
                    return Task.CompletedTask;
                },
                CancellationToken.None).ConfigureAwait(false);

            Assert.That(retrievedMessage, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(retrievedMessage.Id, Is.EqualTo(originalMessage.Message.Id));
                Assert.That(retrievedMessage.Headers["CustomHeader"], Is.EqualTo("CustomValue"));
                Assert.That(retrievedMessage.Body, Is.EqualTo(originalMessage.Message.Body));
            });
        }

        [Test]
        public async Task Should_store_document_in_correct_collection()
        {
            var failedImport = CreateFailedAuditImport("collection-test");

            await failedAuditStorage.SaveFailedAuditImport(failedImport).ConfigureAwait(false);

            var collection = database.GetCollection<BsonDocument>(CollectionNames.FailedAuditImports);
            var count = await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty).ConfigureAwait(false);

            Assert.That(count, Is.EqualTo(1));
        }

        [Test]
        public async Task Should_handle_cancellation_during_processing()
        {
            await failedAuditStorage.SaveFailedAuditImport(CreateFailedAuditImport("cancel-1")).ConfigureAwait(false);
            await failedAuditStorage.SaveFailedAuditImport(CreateFailedAuditImport("cancel-2")).ConfigureAwait(false);
            await failedAuditStorage.SaveFailedAuditImport(CreateFailedAuditImport("cancel-3")).ConfigureAwait(false);

            using var cts = new CancellationTokenSource();
            var processedCount = 0;

            // Cancellation may throw OperationCanceledException from MongoDB driver
            try
            {
                await failedAuditStorage.ProcessFailedMessages(
                    (transportMessage, markComplete, token) =>
                    {
                        processedCount++;
                        if (processedCount == 1)
                        {
                            cts.Cancel();
                        }
                        return Task.CompletedTask;
                    },
                    cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }

            // Should have processed at least one message before cancellation
            Assert.That(processedCount, Is.GreaterThanOrEqualTo(1));
        }

        static FailedAuditImport CreateFailedAuditImport(string messageId)
        {
            return new FailedAuditImport
            {
                Id = $"FailedAuditImports/{Guid.NewGuid()}",
                Message = new FailedTransportMessage
                {
                    Id = messageId,
                    Headers = new Dictionary<string, string>
                    {
                        ["NServiceBus.MessageId"] = messageId,
                        ["NServiceBus.EnclosedMessageTypes"] = "TestMessage"
                    },
                    Body = System.Text.Encoding.UTF8.GetBytes($"Body for {messageId}")
                },
                ExceptionInfo = $"Exception for {messageId}"
            };
        }
    }
}
