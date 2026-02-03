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
    using ServiceControl.Audit.Infrastructure;
    using ServiceControl.Audit.Persistence.MongoDB;
    using ServiceControl.Audit.Persistence.MongoDB.Collections;
    using ServiceControl.Audit.Persistence.UnitOfWork;
    using Infrastructure;

    /// <summary>
    /// Base class for AuditDataStore tests that can run against different MongoDB-compatible products.
    /// </summary>
    public abstract class AuditDataStoreTestsBase
    {
        protected IMongoTestEnvironment Environment { get; private set; }

        IHost host;
        IMongoDatabase database;
        IAuditDataStore dataStore;
        IAuditIngestionUnitOfWorkFactory unitOfWorkFactory;
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
            databaseName = $"datastore_test_{Guid.NewGuid():N}";
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
            dataStore = host.Services.GetRequiredService<IAuditDataStore>();
            unitOfWorkFactory = host.Services.GetRequiredService<IAuditIngestionUnitOfWorkFactory>();
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
        public async Task Should_query_and_return_messages()
        {
            // Insert test messages
            for (int i = 0; i < 5; i++)
            {
                await InsertProcessedMessage($"msg-{i}").ConfigureAwait(false);
            }

            // Query messages
            var result = await dataStore.GetMessages(
                includeSystemMessages: true,
                pagingInfo: new PagingInfo(1, 50),
                sortInfo: new SortInfo("processed_at", "desc"),
                timeSentRange: null,
                cancellationToken: CancellationToken.None).ConfigureAwait(false);

            Assert.Multiple(() =>
            {
                Assert.That(result.Results, Has.Count.EqualTo(5));
                Assert.That(result.QueryStats.TotalCount, Is.EqualTo(5));
            });
        }

        [Test]
        public async Task Queries_should_use_indexes()
        {
            await InsertProcessedMessage("test-msg", endpoint: "TestEndpoint").ConfigureAwait(false);

            // Test endpoint+processedAt compound index
            var explanation = await RunExplainCommand(
                CollectionNames.ProcessedMessages,
                new BsonDocument("messageMetadata.ReceivingEndpoint.Name", "TestEndpoint"),
                new BsonDocument("processedAt", -1),
                50).ConfigureAwait(false);

            AssertIndexUsed(explanation, "endpoint_processedAt");
        }

        async Task InsertProcessedMessage(string messageId, string endpoint = "TestEndpoint")
        {
            var headers = new Dictionary<string, string>
            {
                ["NServiceBus.MessageId"] = messageId,
                ["NServiceBus.ConversationId"] = Guid.NewGuid().ToString(),
                ["NServiceBus.ProcessingStarted"] = DateTime.UtcNow.AddSeconds(-1).ToString("O"),
                ["NServiceBus.ProcessingEnded"] = DateTime.UtcNow.ToString("O"),
                ["$.diagnostics.originating.hostid"] = Guid.NewGuid().ToString(),
                ["NServiceBus.ProcessingEndpoint"] = endpoint,
                ["NServiceBus.ContentType"] = "application/json"
            };

            var endpointDetails = new Dictionary<string, object>
            {
                ["Name"] = endpoint,
                ["HostId"] = Guid.NewGuid(),
                ["Host"] = "localhost"
            };

            var metadata = new Dictionary<string, object>
            {
                ["MessageId"] = messageId,
                ["MessageType"] = "TestMessage",
                ["TimeSent"] = DateTime.UtcNow,
                ["IsSystemMessage"] = false,
                ["ConversationId"] = Guid.NewGuid().ToString(),
                ["ReceivingEndpoint"] = endpointDetails,
                ["SendingEndpoint"] = endpointDetails,
                ["ContentLength"] = 0
            };

            var message = new ProcessedMessage(headers, metadata);

            var unitOfWork = await unitOfWorkFactory.StartNew(1, CancellationToken.None).ConfigureAwait(false);
            try
            {
                await unitOfWork.RecordProcessedMessage(message, default, CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                await unitOfWork.DisposeAsync().ConfigureAwait(false);
            }
        }

        async Task<BsonDocument> RunExplainCommand(string collectionName, BsonDocument filter, BsonDocument sort, int? limit)
        {
            var findCommand = new BsonDocument
            {
                { "find", collectionName },
                { "filter", filter }
            };

            if (sort != null)
            {
                findCommand["sort"] = sort;
            }

            if (limit.HasValue)
            {
                findCommand["limit"] = limit.Value;
            }

            var explainCommand = new BsonDocument
            {
                { "explain", findCommand },
                { "verbosity", "queryPlanner" }
            };

            return await database.RunCommandAsync<BsonDocument>(explainCommand).ConfigureAwait(false);
        }

        static void AssertIndexUsed(BsonDocument explanation, string expectedIndexName)
        {
            var queryPlanner = explanation.GetValue("queryPlanner", null)?.AsBsonDocument;
            Assert.That(queryPlanner, Is.Not.Null, "No queryPlanner in explain output");

            var winningPlan = queryPlanner.GetValue("winningPlan", null)?.AsBsonDocument;
            Assert.That(winningPlan, Is.Not.Null, "No winningPlan in explain output");

            var indexName = FindIndexNameInPlan(winningPlan);

            Assert.That(indexName, Is.EqualTo(expectedIndexName),
                $"Expected query to use index '{expectedIndexName}' but used '{indexName ?? "COLLSCAN"}'");
        }

        static string FindIndexNameInPlan(BsonDocument plan)
        {
            if (plan.TryGetValue("stage", out var stage) && stage.AsString == "IXSCAN")
            {
                if (plan.TryGetValue("indexName", out var indexName))
                {
                    return indexName.AsString;
                }
            }

            if (plan.TryGetValue("inputStage", out var inputStage) && inputStage.IsBsonDocument)
            {
                return FindIndexNameInPlan(inputStage.AsBsonDocument);
            }

            if (plan.TryGetValue("inputStages", out var inputStages) && inputStages.IsBsonArray)
            {
                foreach (var childStage in inputStages.AsBsonArray)
                {
                    if (childStage.IsBsonDocument)
                    {
                        var result = FindIndexNameInPlan(childStage.AsBsonDocument);
                        if (result != null)
                        {
                            return result;
                        }
                    }
                }
            }

            return null;
        }
    }
}
