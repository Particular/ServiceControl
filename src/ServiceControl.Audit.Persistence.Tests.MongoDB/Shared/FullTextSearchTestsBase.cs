namespace ServiceControl.Audit.Persistence.Tests.MongoDB.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using NUnit.Framework;
    using ServiceControl.Audit.Auditing;
    using ServiceControl.Audit.Infrastructure;
    using ServiceControl.Audit.Monitoring;
    using ServiceControl.Audit.Persistence.MongoDB;
    using ServiceControl.Audit.Persistence.UnitOfWork;
    using Infrastructure;

    /// <summary>
    /// Base class for full-text search tests that can run against different MongoDB-compatible products.
    /// </summary>
    public abstract class FullTextSearchTestsBase
    {
        protected IMongoTestEnvironment Environment { get; private set; }

        IHost host;
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
        }

        [TearDown]
        public async Task TearDown()
        {
            if (host != null)
            {
                var clientProvider = host.Services.GetRequiredService<IMongoClientProvider>();
                await clientProvider.Database.Client.DropDatabaseAsync(databaseName).ConfigureAwait(false);

                await host.StopAsync().ConfigureAwait(false);
                host.Dispose();
            }
        }

        [Test]
        public async Task Should_find_message_by_message_id()
        {
            var factory = host.Services.GetRequiredService<IAuditIngestionUnitOfWorkFactory>();
            var dataStore = host.Services.GetRequiredService<IAuditDataStore>();

            var targetMessageId = "unique-target-message-id-12345";
            var message = CreateProcessedMessage("msg-1", targetMessageId, "MyMessageType");

            await IngestMessage(factory, message).ConfigureAwait(false);

            // Search for the specific message ID
            var result = await dataStore.QueryMessages(
                targetMessageId,
                new PagingInfo(1, 50),
                new SortInfo("processed_at", "desc"),
                null,
                CancellationToken.None).ConfigureAwait(false);

            Assert.That(result.Results, Has.Count.EqualTo(1), "Should find exactly one message");
            Assert.That(result.Results[0].MessageId, Is.EqualTo(targetMessageId), "Should find the correct message");
        }

        [Test]
        public async Task Should_find_message_by_message_type()
        {
            var factory = host.Services.GetRequiredService<IAuditIngestionUnitOfWorkFactory>();
            var dataStore = host.Services.GetRequiredService<IAuditDataStore>();

            var targetMessageType = "UniqueTestMessageType";
            var message = CreateProcessedMessage("msg-1", "msg-id-1", targetMessageType);

            await IngestMessage(factory, message).ConfigureAwait(false);

            var result = await dataStore.QueryMessages(
                targetMessageType,
                new PagingInfo(1, 50),
                new SortInfo("processed_at", "desc"),
                null,
                CancellationToken.None).ConfigureAwait(false);

            Assert.That(result.Results, Has.Count.EqualTo(1), "Should find exactly one message");
            Assert.That(result.Results[0].MessageType, Is.EqualTo(targetMessageType), "Should find the correct message type");
        }

        [Test]
        public async Task Should_find_message_by_header_value()
        {
            var factory = host.Services.GetRequiredService<IAuditIngestionUnitOfWorkFactory>();
            var dataStore = host.Services.GetRequiredService<IAuditDataStore>();

            var uniqueHeaderValue = "unique-custom-header-value-xyz";
            var message = CreateProcessedMessage("msg-1", "msg-id-1", "TestMessage");
            message.Headers["CustomHeader"] = uniqueHeaderValue;

            await IngestMessage(factory, message).ConfigureAwait(false);

            var result = await dataStore.QueryMessages(
                uniqueHeaderValue,
                new PagingInfo(1, 50),
                new SortInfo("processed_at", "desc"),
                null,
                CancellationToken.None).ConfigureAwait(false);

            Assert.That(result.Results, Has.Count.EqualTo(1), "Should find message by header value");
        }

        [Test]
        public async Task Should_return_empty_results_for_no_match()
        {
            var factory = host.Services.GetRequiredService<IAuditIngestionUnitOfWorkFactory>();
            var dataStore = host.Services.GetRequiredService<IAuditDataStore>();

            var message = CreateProcessedMessage("msg-1", "msg-id-1", "TestMessage");
            await IngestMessage(factory, message).ConfigureAwait(false);

            var result = await dataStore.QueryMessages(
                "nonexistent-search-term-that-will-not-match",
                new PagingInfo(1, 50),
                new SortInfo("processed_at", "desc"),
                null,
                CancellationToken.None).ConfigureAwait(false);

            Assert.That(result.Results, Is.Empty, "Should return empty results for non-matching search");
        }

        [Test]
        public async Task Should_find_messages_by_endpoint_and_keyword()
        {
            var factory = host.Services.GetRequiredService<IAuditIngestionUnitOfWorkFactory>();
            var dataStore = host.Services.GetRequiredService<IAuditDataStore>();

            var targetEndpoint = "TargetEndpoint";
            var targetMessageType = "SpecificMessageType";

            // Create message for target endpoint with specific type
            var message1 = CreateProcessedMessage("msg-1", "msg-id-1", targetMessageType);
            message1.MessageMetadata["ReceivingEndpoint"] = new EndpointDetails
            {
                Name = targetEndpoint,
                Host = "localhost",
                HostId = Guid.NewGuid()
            };

            // Create message for different endpoint with same type
            var message2 = CreateProcessedMessage("msg-2", "msg-id-2", targetMessageType);
            message2.MessageMetadata["ReceivingEndpoint"] = new EndpointDetails
            {
                Name = "OtherEndpoint",
                Host = "localhost",
                HostId = Guid.NewGuid()
            };

            await IngestMessage(factory, message1).ConfigureAwait(false);
            await IngestMessage(factory, message2).ConfigureAwait(false);

            var result = await dataStore.QueryMessagesByReceivingEndpointAndKeyword(
                targetEndpoint,
                targetMessageType,
                new PagingInfo(1, 50),
                new SortInfo("processed_at", "desc"),
                null,
                CancellationToken.None).ConfigureAwait(false);

            Assert.That(result.Results, Has.Count.EqualTo(1), "Should find exactly one message for endpoint+keyword combination");
            Assert.That(result.Results[0].ReceivingEndpoint?.Name, Is.EqualTo(targetEndpoint), "Should find message from correct endpoint");
        }

        [Test]
        public async Task Should_paginate_search_results()
        {
            var factory = host.Services.GetRequiredService<IAuditIngestionUnitOfWorkFactory>();
            var dataStore = host.Services.GetRequiredService<IAuditDataStore>();

            var sharedMessageType = "PaginatedTestMessage";

            // Create 5 messages with same type
            for (var i = 0; i < 5; i++)
            {
                var message = CreateProcessedMessage($"msg-{i}", $"msg-id-{i}", sharedMessageType);
                await IngestMessage(factory, message).ConfigureAwait(false);
            }

            // Get first page (2 results)
            var page1 = await dataStore.QueryMessages(
                sharedMessageType,
                new PagingInfo(1, 2),
                new SortInfo("processed_at", "desc"),
                null,
                CancellationToken.None).ConfigureAwait(false);

            // Get second page
            var page2 = await dataStore.QueryMessages(
                sharedMessageType,
                new PagingInfo(2, 2),
                new SortInfo("processed_at", "desc"),
                null,
                CancellationToken.None).ConfigureAwait(false);

            Assert.Multiple(() =>
            {
                Assert.That(page1.Results, Has.Count.EqualTo(2), "First page should have 2 results");
                Assert.That(page2.Results, Has.Count.EqualTo(2), "Second page should have 2 results");
                Assert.That(page1.QueryStats.TotalCount, Is.EqualTo(5), "Total count should be 5");
            });
        }

        [Test]
        public async Task Should_find_message_by_body_content()
        {
            var factory = host.Services.GetRequiredService<IAuditIngestionUnitOfWorkFactory>();
            var dataStore = host.Services.GetRequiredService<IAuditDataStore>();

            var uniqueBodyContent = "UniqueSearchableBodyContent12345";
            var bodyJson = $"{{ \"data\": \"{uniqueBodyContent}\" }}";
            var message = CreateProcessedMessage("msg-body-search", "msg-id-body", "TestMessage");

            await IngestMessageWithBody(factory, message, Encoding.UTF8.GetBytes(bodyJson)).ConfigureAwait(false);

            var result = await dataStore.QueryMessages(
                uniqueBodyContent,
                new PagingInfo(1, 50),
                new SortInfo("processed_at", "desc"),
                null,
                CancellationToken.None).ConfigureAwait(false);

            Assert.That(result.Results, Has.Count.EqualTo(1), "Should find message by body content");
        }

        [Test]
        public async Task Should_not_find_message_with_binary_body_by_content()
        {
            var factory = host.Services.GetRequiredService<IAuditIngestionUnitOfWorkFactory>();
            var dataStore = host.Services.GetRequiredService<IAuditDataStore>();

            // Binary content with invalid UTF-8 won't be indexed
            var binaryContent = new byte[] { 0x00, 0x01, 0x02, 0xFF, 0xFE, 0xFD };
            var message = CreateProcessedMessage("msg-binary", "msg-id-binary", "BinaryMessage");

            await IngestMessageWithBody(factory, message, binaryContent).ConfigureAwait(false);

            // Search for binary bytes (won't match since binary content isn't indexed)
            var result = await dataStore.QueryMessages(
                "0xFF0xFE",
                new PagingInfo(1, 50),
                new SortInfo("processed_at", "desc"),
                null,
                CancellationToken.None).ConfigureAwait(false);

            Assert.That(result.Results, Is.Empty, "Binary body content should not be searchable");
        }

        static async Task IngestMessage(IAuditIngestionUnitOfWorkFactory factory, ProcessedMessage message)
        {
            var unitOfWork = await factory.StartNew(10, CancellationToken.None).ConfigureAwait(false);
            try
            {
                await unitOfWork.RecordProcessedMessage(message, ReadOnlyMemory<byte>.Empty, CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                await unitOfWork.DisposeAsync().ConfigureAwait(false);
            }
        }

        static async Task IngestMessageWithBody(IAuditIngestionUnitOfWorkFactory factory, ProcessedMessage message, byte[] body)
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

        static ProcessedMessage CreateProcessedMessage(string id, string messageId, string messageType)
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
                ["MessageType"] = messageType,
                ["TimeSent"] = DateTime.UtcNow,
                ["IsSystemMessage"] = false,
                ["ReceivingEndpoint"] = new EndpointDetails
                {
                    Name = "TestEndpoint",
                    Host = "localhost",
                    HostId = Guid.NewGuid()
                }
            };

            return new ProcessedMessage(headers, metadata) { Id = id };
        }
    }
}
