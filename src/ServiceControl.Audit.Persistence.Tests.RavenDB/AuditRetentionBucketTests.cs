namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Auditing;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Time.Testing;
    using NServiceBus;
    using NUnit.Framework;
    using ServiceControl.Audit.Infrastructure;
    using ServiceControl.Audit.Monitoring;
    using ServiceControl.Audit.Persistence.RavenDB;
    using ServiceControl.Audit.Persistence.RavenDB.AuditRetentionBuckets;
    using ServiceControl.SagaAudit;

    /// <summary>
    /// Raven integration tests for the opt-in audit retention bucket mode. They run against the shared
    /// embedded RavenDB server and drive the bucket manager through the real DI container, the real
    /// ingestion unit of work and the real data store.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    class AuditRetentionBucketTests : PersistenceTestFixture
    {
        // Fixed UTC reference time. A FakeTimeProvider is registered in the DI container (replacing
        // TimeProvider.System) so rollover and cleanup decisions are deterministic: hourly buckets are
        // derived from the provider's current time via AuditRetentionBucketManager.
        static readonly DateTime BaseTime = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        FakeTimeProvider timeProvider;
        GatedSessionProvider gatedSessionProvider;

        public override Task Setup()
        {
            timeProvider = new FakeTimeProvider(new DateTimeOffset(BaseTime));

            SetSettings = s =>
            {
                s.PersisterSpecificSettings[RavenPersistenceConfiguration.EnableAuditRetentionBucketsKey] = "true";

                // Keep the background cleanup timer's first tick (start + interval) outside the window
                // these tests advance through, so cleanup only ever runs when a test invokes it explicitly.
                s.PersisterSpecificSettings[RavenPersistenceConfiguration.ExpirationProcessTimerInSecondsKey] = "10800";
            };

            ConfigureServices = services =>
            {
                services.AddSingleton<TimeProvider>(timeProvider);

                // Replaces the real session provider so the concurrency test can prove bounded fan-out
                // without timing assertions. The gate is inert until a test arms it.
                gatedSessionProvider = new GatedSessionProvider();
                services.AddSingleton<IRavenSessionProvider>(sp =>
                {
                    gatedSessionProvider.Initialize(sp.GetRequiredService<IRavenDocumentStoreProvider>());
                    return gatedSessionProvider;
                });
            };

            return base.Setup();
        }

        [Test]
        public async Task Bucket_mode_creates_distinct_collections_and_indexes_per_bucket()
        {
            timeProvider.SetUtcNow(new DateTimeOffset(BaseTime));
            await IngestMessages(MakeMessage("msg-1", BaseTime.AddMinutes(5)));
            await IngestSagaSnapshots(new SagaSnapshot { SagaId = Guid.NewGuid(), StateAfterChange = "saga-state" });

            timeProvider.SetUtcNow(new DateTimeOffset(BaseTime.AddHours(2)));
            await IngestMessages(MakeMessage("msg-2", BaseTime.AddHours(2).AddMinutes(5)));
            await IngestSagaSnapshots(new SagaSnapshot { SagaId = Guid.NewGuid(), StateAfterChange = "saga-state" });

            await configuration.CompleteDBOperation();

            var collectionStats = await configuration.DocumentStore.Maintenance.SendAsync(new Raven.Client.Documents.Operations.GetCollectionStatisticsOperation());
            var indexNames = (await configuration.DocumentStore.Maintenance.SendAsync(new Raven.Client.Documents.Operations.Indexes.GetIndexesOperation(0, int.MaxValue)))
                .Select(i => i.Name)
                .ToArray();

            using var session = configuration.DocumentStore.OpenAsyncSession();
            var catalog = await session.LoadAsync<AuditRetentionBucketCatalog>(AuditRetentionBucketCatalog.DocumentId);

            Assert.Multiple(() =>
            {
                // Distinct per-bucket collections for both document types.
                Assert.That(collectionStats.Collections.TryGetValue("ProcessedMessages_20260101_00", out var pm00), Is.True);
                Assert.That(pm00, Is.GreaterThan(0));
                Assert.That(collectionStats.Collections.TryGetValue("SagaSnapshots_20260101_00", out var ss00), Is.True);
                Assert.That(ss00, Is.GreaterThan(0));
                Assert.That(collectionStats.Collections.TryGetValue("ProcessedMessages_20260101_02", out var pm02), Is.True);
                Assert.That(pm02, Is.GreaterThan(0));
                Assert.That(collectionStats.Collections.TryGetValue("SagaSnapshots_20260101_02", out var ss02), Is.True);
                Assert.That(ss02, Is.GreaterThan(0));

                // Dedicated static indexes per bucket.
                Assert.That(indexNames, Does.Contain("MessagesViewIndexWithFullTextSearch_20260101_00"));
                Assert.That(indexNames, Does.Contain("MessagesViewIndexWithFullTextSearch_20260101_02"));
                Assert.That(indexNames, Does.Contain("SagaDetailsIndex_20260101_00"));
                Assert.That(indexNames, Does.Contain("SagaDetailsIndex_20260101_02"));

                // The shared legacy indexes are not created in bucket mode.
                Assert.That(indexNames, Does.Not.Contain("MessagesViewIndexWithFullTextSearch"));
                Assert.That(indexNames, Does.Not.Contain("MessagesViewIndex"));
                Assert.That(indexNames, Does.Not.Contain("SagaDetailsIndex"));

                // Durable catalog tracks both buckets.
                Assert.That(catalog, Is.Not.Null);
                Assert.That(catalog.Buckets.Select(b => b.Key), Is.EquivalentTo(new[] { "20260101_00", "20260101_02" }));
            });
        }

        [Test]
        public async Task Bucket_mode_reads_merge_across_buckets()
        {
            timeProvider.SetUtcNow(new DateTimeOffset(BaseTime));
            await IngestMessages(
                MakeMessage("msg-a", BaseTime.AddMinutes(10), conversationId: "conv-shared"),
                MakeMessage("msg-b", BaseTime.AddMinutes(20), conversationId: "conv-shared"));

            timeProvider.SetUtcNow(new DateTimeOffset(BaseTime.AddHours(2)));
            await IngestMessages(MakeMessage("msg-c", BaseTime.AddHours(2).AddMinutes(10), conversationId: "conv-shared"));

            var allMessages = await DataStore.GetMessages(
                includeSystemMessages: true,
                new PagingInfo(page: 1, pageSize: 10),
                new SortInfo("time_sent", "asc"),
                timeSentRange: null,
                TestContext.CurrentContext.CancellationToken);

            var byConversation = await DataStore.QueryMessagesByConversationId(
                "conv-shared",
                new PagingInfo(page: 1, pageSize: 10),
                new SortInfo("time_sent", "asc"),
                TestContext.CurrentContext.CancellationToken);

            Assert.Multiple(() =>
            {
                Assert.That(allMessages.Results.Select(m => m.MessageId), Is.EqualTo(new[] { "msg-a", "msg-b", "msg-c" }));
                Assert.That(allMessages.QueryStats.TotalCount, Is.EqualTo(3));
                Assert.That(byConversation.Results.Select(m => m.MessageId), Is.EqualTo(new[] { "msg-a", "msg-b", "msg-c" }));
                Assert.That(byConversation.QueryStats.TotalCount, Is.EqualTo(3));
            });
        }

        [Test]
        public async Task Bucket_mode_paging_is_correct_across_buckets()
        {
            timeProvider.SetUtcNow(new DateTimeOffset(BaseTime));
            await IngestMessages(
                MakeMessage("msg-1", BaseTime.AddMinutes(1)),
                MakeMessage("msg-2", BaseTime.AddMinutes(2)),
                MakeMessage("msg-3", BaseTime.AddMinutes(3)));

            timeProvider.SetUtcNow(new DateTimeOffset(BaseTime.AddHours(2)));
            await IngestMessages(
                MakeMessage("msg-4", BaseTime.AddHours(2).AddMinutes(1)),
                MakeMessage("msg-5", BaseTime.AddHours(2).AddMinutes(2)));

            var page1 = await DataStore.GetMessages(true, new PagingInfo(page: 1, pageSize: 2), new SortInfo("time_sent", "asc"), null, TestContext.CurrentContext.CancellationToken);
            var page2 = await DataStore.GetMessages(true, new PagingInfo(page: 2, pageSize: 2), new SortInfo("time_sent", "asc"), null, TestContext.CurrentContext.CancellationToken);
            var page3 = await DataStore.GetMessages(true, new PagingInfo(page: 3, pageSize: 2), new SortInfo("time_sent", "asc"), null, TestContext.CurrentContext.CancellationToken);

            Assert.Multiple(() =>
            {
                // Page 2 spans the bucket boundary (msg-3 from the first bucket, msg-4 from the second).
                Assert.That(page1.Results.Select(m => m.MessageId), Is.EqualTo(new[] { "msg-1", "msg-2" }));
                Assert.That(page1.QueryStats.TotalCount, Is.EqualTo(5));
                Assert.That(page2.Results.Select(m => m.MessageId), Is.EqualTo(new[] { "msg-3", "msg-4" }));
                Assert.That(page2.QueryStats.TotalCount, Is.EqualTo(5));
                Assert.That(page3.Results.Select(m => m.MessageId), Is.EqualTo(new[] { "msg-5" }));
                Assert.That(page3.QueryStats.TotalCount, Is.EqualTo(5));
            });
        }

        [Test]
        public async Task Bucket_mode_queries_fan_out_concurrently_across_buckets()
        {
            timeProvider.SetUtcNow(new DateTimeOffset(BaseTime));
            await IngestMessages(MakeMessage("msg-1", BaseTime.AddMinutes(1)));

            timeProvider.SetUtcNow(new DateTimeOffset(BaseTime.AddHours(2)));
            await IngestMessages(MakeMessage("msg-2", BaseTime.AddHours(2).AddMinutes(1)));

            gatedSessionProvider.Arm();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var operation = DataStore.GetMessages(true, new PagingInfo(), new SortInfo("time_sent", "asc"), null, cts.Token);

            try
            {
                // Deterministic concurrency barrier: with bounded concurrent fan-out, both bucket
                // queries request their own session before either executes. A sequential
                // implementation would only ever have one session request in flight, so this wait
                // would time out instead of completing.
                await gatedSessionProvider.WhenTwoSessionsRequested.WaitAsync(TimeSpan.FromSeconds(5), cts.Token);
            }
            finally
            {
                gatedSessionProvider.Release();
            }

            var result = await operation;

            Assert.Multiple(() =>
            {
                Assert.That(result.Results.Select(m => m.MessageId), Is.EqualTo(new[] { "msg-1", "msg-2" }));
                Assert.That(result.QueryStats.TotalCount, Is.EqualTo(2));
            });
        }

        [Test]
        public async Task Bucket_mode_saga_history_merges_changes_across_buckets()
        {
            var sagaId = Guid.NewGuid();

            timeProvider.SetUtcNow(new DateTimeOffset(BaseTime));
            await IngestSagaSnapshots(new SagaSnapshot
            {
                SagaId = sagaId,
                SagaType = "MySaga",
                StateAfterChange = "state-1",
                FinishTime = BaseTime.AddMinutes(5)
            });

            timeProvider.SetUtcNow(new DateTimeOffset(BaseTime.AddHours(2)));
            await IngestSagaSnapshots(new SagaSnapshot
            {
                SagaId = sagaId,
                SagaType = "MySaga",
                StateAfterChange = "state-2",
                FinishTime = BaseTime.AddHours(2).AddMinutes(5)
            });

            var result = await DataStore.QuerySagaHistoryById(sagaId, TestContext.CurrentContext.CancellationToken);

            Assert.Multiple(() =>
            {
                Assert.That(result.Results, Is.Not.Null);
                Assert.That(result.Results.SagaId, Is.EqualTo(sagaId));
                Assert.That(result.Results.Changes.Select(c => c.StateAfterChange), Is.EquivalentTo(new[] { "state-1", "state-2" }));
                // All fragments merge into a single SagaHistory, so the total is 1 (legacy mode semantics),
                // not the sum of per-bucket index totals.
                Assert.That(result.QueryStats.TotalCount, Is.EqualTo(1));
            });
        }

        [Test]
        public async Task Bucket_mode_cleanup_deletes_expired_bucket_indexes_and_collections_but_retains_active_bucket()
        {
            timeProvider.SetUtcNow(new DateTimeOffset(BaseTime));
            await IngestMessages(MakeMessage("old-msg", BaseTime.AddMinutes(5)));
            await IngestSagaSnapshots(new SagaSnapshot { SagaId = Guid.NewGuid(), StateAfterChange = "old-saga-state" });

            timeProvider.SetUtcNow(new DateTimeOffset(BaseTime.AddHours(2)));
            await IngestMessages(MakeMessage("new-msg", BaseTime.AddHours(2).AddMinutes(5)));

            var manager = ServiceProvider.GetRequiredService<AuditRetentionBucketManager>();
            await manager.RunCleanup(TestContext.CurrentContext.CancellationToken);

            var collectionStats = await configuration.DocumentStore.Maintenance.SendAsync(new Raven.Client.Documents.Operations.GetCollectionStatisticsOperation());
            var indexNames = (await configuration.DocumentStore.Maintenance.SendAsync(new Raven.Client.Documents.Operations.Indexes.GetIndexesOperation(0, int.MaxValue)))
                .Select(i => i.Name)
                .ToArray();

            var activeBuckets = await manager.GetActiveBuckets(TestContext.CurrentContext.CancellationToken);

            // Expired bucket: cleanup removed its dedicated indexes first, then its collections.
            Assert.Multiple(() =>
            {
                Assert.That(indexNames, Does.Not.Contain("MessagesViewIndexWithFullTextSearch_20260101_00"));
                Assert.That(indexNames, Does.Not.Contain("SagaDetailsIndex_20260101_00"));

                collectionStats.Collections.TryGetValue("ProcessedMessages_20260101_00", out var oldProcessedCount);
                Assert.That(oldProcessedCount, Is.EqualTo(0));
                collectionStats.Collections.TryGetValue("SagaSnapshots_20260101_00", out var oldSagaCount);
                Assert.That(oldSagaCount, Is.EqualTo(0));

                // The active bucket is retained in the catalog with its indexes and collections.
                Assert.That(activeBuckets.Select(b => b.Key), Is.EqualTo(new[] { "20260101_02" }));
                Assert.That(indexNames, Does.Contain("MessagesViewIndexWithFullTextSearch_20260101_02"));
                Assert.That(indexNames, Does.Contain("SagaDetailsIndex_20260101_02"));
                Assert.That(collectionStats.Collections.TryGetValue("ProcessedMessages_20260101_02", out var activeCount), Is.True);
                Assert.That(activeCount, Is.GreaterThan(0));
            });

            // The retained bucket's messages are still readable after cleanup.
            var result = await DataStore.GetMessages(true, new PagingInfo(), new SortInfo("time_sent", "asc"), null, TestContext.CurrentContext.CancellationToken);
            Assert.That(result.Results.Select(m => m.MessageId), Is.EqualTo(new[] { "new-msg" }));
        }

        ProcessedMessage MakeMessage(string messageId, DateTime timeSent, string endpoint = "SomeEndpoint", string conversationId = null)
        {
            conversationId ??= Guid.NewGuid().ToString();

            var metadata = new Dictionary<string, object>
            {
                { "MessageId", messageId },
                { "MessageIntent", MessageIntent.Send },
                { "CriticalTime", TimeSpan.FromSeconds(5) },
                { "ProcessingTime", TimeSpan.FromSeconds(1) },
                { "DeliveryTime", TimeSpan.FromSeconds(4) },
                { "IsSystemMessage", false },
                { "MessageType", "MyMessageType" },
                { "IsRetried", false },
                { "ConversationId", conversationId },
                { "ContentLength", 10 },
                { "TimeSent", timeSent },
                { "ReceivingEndpoint", new EndpointDetails { Name = endpoint } },
                { "SendingEndpoint", new EndpointDetails { Name = endpoint } }
            };

            var headers = new Dictionary<string, string>
            {
                { Headers.MessageId, messageId },
                { Headers.ProcessingEndpoint, endpoint },
                { Headers.MessageIntent, MessageIntent.Send.ToString() },
                { Headers.ConversationId, conversationId },
                { Headers.ProcessingStarted, DateTimeOffsetHelper.ToWireFormattedString(new DateTimeOffset(timeSent)) },
                { Headers.ProcessingEnded, DateTimeOffsetHelper.ToWireFormattedString(new DateTimeOffset(timeSent.AddSeconds(5))) }
            };

            return new ProcessedMessage(headers, metadata);
        }

        async Task IngestMessages(params ProcessedMessage[] messages)
        {
            var unitOfWork = await StartAuditUnitOfWork(messages.Length);
            foreach (var message in messages)
            {
                await unitOfWork.RecordProcessedMessage(message);
            }
            await unitOfWork.DisposeAsync();
            await configuration.CompleteDBOperation();
        }

        async Task IngestSagaSnapshots(params SagaSnapshot[] snapshots)
        {
            var unitOfWork = await StartAuditUnitOfWork(snapshots.Length);
            foreach (var snapshot in snapshots)
            {
                await unitOfWork.RecordSagaSnapshot(snapshot);
            }
            await unitOfWork.DisposeAsync();
            await configuration.CompleteDBOperation();
        }
    }

    /// <summary>
    /// Verifies the startup fail-fast guard that prevents enabling bucket mode on a populated legacy
    /// Audit database.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    class AuditRetentionBucketLegacyDataGuardTests : PersistenceTestFixture
    {
        [Test]
        public async Task Bucket_mode_setup_succeeds_on_empty_database()
        {
            await new DatabaseSetup(BuildBucketModeConfiguration()).Execute(configuration.DocumentStore, TestContext.CurrentContext.CancellationToken);
        }

        [Test]
        public async Task Bucket_mode_setup_fails_fast_when_legacy_processed_messages_exist()
        {
            await IngestProcessedMessagesAudits(MakeMessage("legacy-msg"));

            var exception = Assert.ThrowsAsync<InvalidOperationException>(() =>
                new DatabaseSetup(BuildBucketModeConfiguration()).Execute(configuration.DocumentStore, TestContext.CurrentContext.CancellationToken));

            Assert.Multiple(() =>
            {
                Assert.That(exception.Message, Does.Contain("empty or new database"));
                Assert.That(exception.Message, Does.Contain("ProcessedMessage"));
            });
        }

        [Test]
        public async Task Bucket_mode_setup_fails_fast_when_legacy_saga_snapshots_exist()
        {
            await IngestSagaAudits(new SagaSnapshot { SagaId = Guid.NewGuid() });

            var exception = Assert.ThrowsAsync<InvalidOperationException>(() =>
                new DatabaseSetup(BuildBucketModeConfiguration()).Execute(configuration.DocumentStore, TestContext.CurrentContext.CancellationToken));

            Assert.That(exception.Message, Does.Contain("SagaSnapshot"));
        }

        DatabaseConfiguration BuildBucketModeConfiguration() => new(
            configuration.DocumentStore.Database,
            expirationProcessTimerInSeconds: 60,
            enableFullTextSearch: true,
            auditRetentionPeriod: TimeSpan.FromHours(1),
            maxBodySizeToStore: 100000,
            dataSpaceRemainingThreshold: 5,
            minimumStorageLeftRequiredForIngestion: 5,
            new ServerConfiguration("http://localhost:12345"),
            bulkInsertCommitTimeout: TimeSpan.FromSeconds(60),
            enableAuditRetentionBuckets: true);

        ProcessedMessage MakeMessage(string messageId = null)
        {
            messageId ??= Guid.NewGuid().ToString();

            var metadata = new Dictionary<string, object>
            {
                { "MessageId", messageId },
                { "MessageIntent", MessageIntent.Send },
                { "CriticalTime", TimeSpan.FromSeconds(5) },
                { "ProcessingTime", TimeSpan.FromSeconds(1) },
                { "DeliveryTime", TimeSpan.FromSeconds(4) },
                { "IsSystemMessage", false },
                { "MessageType", "MyMessageType" },
                { "IsRetried", false },
                { "ConversationId", Guid.NewGuid().ToString() },
                { "ContentLength", 10 },
                { "TimeSent", DateTime.UtcNow },
                { "ReceivingEndpoint", new EndpointDetails { Name = "SomeEndpoint" } },
                { "SendingEndpoint", new EndpointDetails { Name = "SomeEndpoint" } }
            };

            var headers = new Dictionary<string, string>
            {
                { Headers.MessageId, messageId },
                { Headers.ProcessingEndpoint, "SomeEndpoint" },
                { Headers.MessageIntent, MessageIntent.Send.ToString() },
                { Headers.ConversationId, Guid.NewGuid().ToString() },
                { Headers.ProcessingStarted, DateTimeOffsetHelper.ToWireFormattedString(DateTimeOffset.UtcNow) },
                { Headers.ProcessingEnded, DateTimeOffsetHelper.ToWireFormattedString(DateTimeOffset.UtcNow) }
            };

            return new ProcessedMessage(headers, metadata);
        }

        async Task IngestProcessedMessagesAudits(params ProcessedMessage[] processedMessages)
        {
            var unitOfWork = await StartAuditUnitOfWork(processedMessages.Length);
            foreach (var processedMessage in processedMessages)
            {
                await unitOfWork.RecordProcessedMessage(processedMessage);
            }
            await unitOfWork.DisposeAsync();
            await configuration.CompleteDBOperation();
        }

        async Task IngestSagaAudits(params SagaSnapshot[] snapshots)
        {
            var unitOfWork = await StartAuditUnitOfWork(snapshots.Length);
            foreach (var snapshot in snapshots)
            {
                await unitOfWork.RecordSagaSnapshot(snapshot);
            }
            await unitOfWork.DisposeAsync();
            await configuration.CompleteDBOperation();
        }
    }
}
