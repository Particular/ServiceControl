namespace ServiceControl.Persistence.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.Extensions.Time.Testing;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.Infrastructure.Auth;
    using ServiceControl.Infrastructure.BackgroundTasks;
    using ServiceControl.Infrastructure.DomainEvents;
    using ServiceControl.MessageFailures;
    using ServiceControl.Persistence;
    using ServiceControl.Persistence.Tests.Recoverability;
    using ServiceControl.Recoverability;
    using ServiceControl.Transports;
    using static ServiceControl.Recoverability.RecoverabilityComponent;
    using QueueAddress = NServiceBus.Transport.QueueAddress;

    [NonParallelizable]
    class RetryStateTests : PersistenceTestBase
    {
        readonly FakeTimeProvider fakeTime = new(DateTimeOffset.UtcNow);

        [Test]
        public async Task When_a_group_is_processed_it_is_set_to_the_Preparing_state()
        {
            var domainEvents = new FakeDomainEvents();
            var retryManager = new RetryingManager(domainEvents, TestRetryMetrics.Create(fakeTime), NullLogger<RetryingManager>.Instance, fakeTime);

            await CreateAFailedMessageAndMarkAsPartOfRetryBatch(retryManager, "Test-group", true, 1);
            var status = retryManager.GetStatusForRetryOperation("Test-group", RetryType.FailureGroup);

            Assert.That(status.RetryState, Is.EqualTo(RetryState.Preparing));
        }

        [Test]
        public async Task When_a_bulk_retry_is_processed_the_operation_records_when_it_was_asked_for()
        {
            var askedAt = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var retryManager = new RetryingManager(new FakeDomainEvents(), TestRetryMetrics.Create(fakeTime), NullLogger<RetryingManager>.Instance, fakeTime);

            await CreateAFailedMessageAndMarkAsPartOfRetryBatch(retryManager, "Test-group", true, null, null, askedAt, Guid.NewGuid().ToString());

            var operation = retryManager.GetStatusForRetryOperation("Test-group", RetryType.FailureGroup);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(operation.Started, Is.EqualTo(askedAt), "the bulk route carries the time the operator asked on the request, and used to drop it on the way to the operation");
                Assert.That(operation.Originator, Is.EqualTo("Test-Context"), "without this the history row has nothing to describe the retry with");
            }
        }

        [Test]
        public async Task When_a_single_message_is_retried_the_operation_and_the_batch_agree_on_when_it_started()
        {
            var clock = new FakeTimeProvider(new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero));
            var retryManager = new RetryingManager(new FakeDomainEvents(), TestRetryMetrics.Create(clock), NullLogger<RetryingManager>.Instance, clock);
            var messageId = Guid.NewGuid().ToString();

            await InsertUnresolvedFailedMessages("Test-group", messageId);

            var gateway = new CustomRetriesGateway(true, RetryBatchStore, retryManager, clock);
            await gateway.StartRetryForSingleMessage(messageId);
            await CompleteDatabaseOperation();

            var operation = retryManager.GetStatusForRetryOperation(messageId, RetryType.SingleMessage);
            var batchGroup = (await RetryBatchStore.GetAvailableBatchGroups()).Single();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(operation.Started, Is.EqualTo(clock.GetUtcNow().UtcDateTime), "a single-message retry used to record 01 Jan 0001 as its start time");
                Assert.That(batchGroup.StartTime, Is.EqualTo(operation.Started), "the batch is what the start time is rebuilt from after a restart, so the two must not drift apart");
            }
        }

        [Test]
        public async Task When_a_group_is_prepared_and_SC_is_started_the_group_is_marked_as_failed()
        {
            var domainEvents = new FakeDomainEvents();
            var retryManager = new RetryingManager(domainEvents, TestRetryMetrics.Create(fakeTime), NullLogger<RetryingManager>.Instance, fakeTime);

            await CreateAFailedMessageAndMarkAsPartOfRetryBatch(retryManager, "Test-group", false, 1);

            var documentManager = new CustomRetryDocumentManager(false, RetryBatchStore, retryManager);

            var orphanage = new AdoptOrphanBatchesFromPreviousSessionHostedService(documentManager, new AsyncTimer(), NullLogger<AdoptOrphanBatchesFromPreviousSessionHostedService>.Instance);
            await orphanage.AdoptOrphanedBatchesAsync();
            await CompleteDatabaseOperation();

            var status = retryManager.GetStatusForRetryOperation("Test-group", RetryType.FailureGroup);
            Assert.That(status.Failed, Is.True);
        }

        [Test]
        public async Task When_the_dequeuer_is_created_then_the_error_address_is_cached()
        {
            var domainEvents = new FakeDomainEvents();
            var errorQueueNameCache = new ErrorQueueNameCache();
            var transportInfrastructure = new TestTransportInfrastructure(new Dictionary<string, IMessageReceiver>
            {
                ["TestEndpoint.staging"] = null
            })
            {
                TransportAddress = "TestAddress"
            };

            var transportCustomization = new TestTransportCustomization { TransportInfrastructure = transportInfrastructure };

            var testReturnToSenderDequeuer = new TestReturnToSenderDequeuer(new ReturnToSender(BodyStorage, NullLogger<ReturnToSender>.Instance), FailedMessageLifecycleStore, domainEvents, "TestEndpoint",
                errorQueueNameCache, transportCustomization);

            await testReturnToSenderDequeuer.StartAsync(new CancellationToken());

            Assert.That(errorQueueNameCache.ResolvedErrorAddress, Is.EqualTo(transportInfrastructure.TransportAddress));
        }

        [Test]
        public async Task When_a_group_is_prepared_with_three_batches_and_SC_is_restarted_while_the_first_group_is_being_forwarded_then_the_count_still_matches()
        {
            var domainEvents = new FakeDomainEvents();
            var retryManager = new RetryingManager(domainEvents, TestRetryMetrics.Create(fakeTime), NullLogger<RetryingManager>.Instance, fakeTime);

            await CreateAFailedMessageAndMarkAsPartOfRetryBatch(retryManager, "Test-group", true, 2001);

            var sender = new TestSender();
            var processor = new RetryProcessor(
                RetryStagingStore,
                MessageRedirectsDataStore,
                domainEvents,
                new TestReturnToSenderDequeuer(
                    new ReturnToSender(BodyStorage, NullLogger<ReturnToSender>.Instance),
                    FailedMessageLifecycleStore,
                    domainEvents,
                    "TestEndpoint",
                    new ErrorQueueNameCache(),
                    new TestTransportCustomization()),
                retryManager,
                TestRetryMetrics.Create(fakeTime), new Lazy<IMessageDispatcher>(() => sender),
                new RecordingMessageActionAuditLog(),
                NullLogger<RetryProcessor>.Instance);

            // Needs index RetryBatches_ByStatus_ReduceInitialBatchSize
            await CompleteDatabaseOperation();

            await processor.ProcessBatches(); // mark ready

            // Simulate SC restart
            retryManager = new RetryingManager(domainEvents, TestRetryMetrics.Create(fakeTime), NullLogger<RetryingManager>.Instance, fakeTime);

            var documentManager = new CustomRetryDocumentManager(false, RetryBatchStore, retryManager);

            await documentManager.RebuildRetryOperationState();

            processor = new RetryProcessor(
                RetryStagingStore,
                MessageRedirectsDataStore,
                domainEvents,
                new TestReturnToSenderDequeuer(
                    new ReturnToSender(BodyStorage, NullLogger<ReturnToSender>.Instance),
                    FailedMessageLifecycleStore,
                    domainEvents,
                    "TestEndpoint",
                    new ErrorQueueNameCache(),
                    new TestTransportCustomization()),
                retryManager,
                TestRetryMetrics.Create(fakeTime), new Lazy<IMessageDispatcher>(() => sender),
                new RecordingMessageActionAuditLog(),
                NullLogger<RetryProcessor>.Instance);

            await processor.ProcessBatches();

            var status = retryManager.GetStatusForRetryOperation("Test-group", RetryType.FailureGroup);
            Assert.That(status.TotalNumberOfMessages, Is.EqualTo(2001));
        }

        [Test]
        public async Task When_a_group_is_forwarded_the_status_is_Completed()
        {
            var domainEvents = new FakeDomainEvents();
            var retryManager = new RetryingManager(domainEvents, TestRetryMetrics.Create(fakeTime), NullLogger<RetryingManager>.Instance, fakeTime);

            await CreateAFailedMessageAndMarkAsPartOfRetryBatch(retryManager, "Test-group", true, 1);

            var sender = new TestSender();

            var returnToSender = new TestReturnToSenderDequeuer(new ReturnToSender(BodyStorage, NullLogger<ReturnToSender>.Instance), FailedMessageLifecycleStore, domainEvents, "TestEndpoint", new ErrorQueueNameCache(), new TestTransportCustomization());
            var processor = new RetryProcessor(RetryStagingStore, MessageRedirectsDataStore, domainEvents, returnToSender, retryManager, TestRetryMetrics.Create(fakeTime), new Lazy<IMessageDispatcher>(() => sender), new RecordingMessageActionAuditLog(), NullLogger<RetryProcessor>.Instance);

            await processor.ProcessBatches(); // mark ready
            await processor.ProcessBatches();

            var status = retryManager.GetStatusForRetryOperation("Test-group", RetryType.FailureGroup);
            Assert.That(status.RetryState, Is.EqualTo(RetryState.Completed));
        }

        [Test]
        public async Task When_a_staged_batch_has_nothing_left_to_stage_it_is_discarded()
        {
            var batchId = await StageBatchWithoutMessages();

            var processor = CreateProcessor(new FakeDomainEvents(), new TestSender());

            await processor.ProcessBatches();
            await CompleteDatabaseOperation();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(await RetryStagingStore.GetStagingBatch(), Is.Null);
                Assert.That(await RetryStagingStore.GetBatch(batchId, CancellationToken.None), Is.Null);
                Assert.That(await RetryStagingStore.GetForwardingBatchId(), Is.Null);
            }
        }

        [Test]
        public async Task When_the_batch_being_forwarded_is_gone_the_forwarding_pointer_is_cleared()
        {
            var batchId = await StageBatchWithoutMessages();

            await RetryStagingStore.MarkBatchAsForwarding(batchId, "staging-1", []);
            await RetryStagingStore.DiscardBatch(batchId);
            await CompleteDatabaseOperation();

            var processor = CreateProcessor(new FakeDomainEvents(), new TestSender());

            await processor.ProcessBatches();
            await CompleteDatabaseOperation();

            Assert.That(await RetryStagingStore.GetForwardingBatchId(), Is.Null);
        }

        [Test]
        public async Task When_there_is_one_poison_message_it_is_removed_from_batch_and_the_status_is_Complete()
        {
            var domainEvents = new FakeDomainEvents();
            var retryManager = new RetryingManager(domainEvents, TestRetryMetrics.Create(fakeTime), NullLogger<RetryingManager>.Instance, fakeTime);

            var ids = new[] { Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString() };
            var poisonRecordId = PersistenceTestsContext.GenerateFailedMessageRecordId(ids[1]);

            await CreateAFailedMessageAndMarkAsPartOfRetryBatch(retryManager, "Test-group", true, ids);

            var sender = new TestSender
            {
                Callback = operation =>
                {
                    //Always fails staging the second message
                    if (operation.Message.MessageId == poisonRecordId)
                    {
                        throw new Exception("Simulated");
                    }
                }
            };

            var returnToSender = new TestReturnToSenderDequeuer(new ReturnToSender(BodyStorage, NullLogger<ReturnToSender>.Instance), FailedMessageLifecycleStore, domainEvents, "TestEndpoint", new ErrorQueueNameCache(), new TestTransportCustomization());
            var processor = new RetryProcessor(RetryStagingStore, MessageRedirectsDataStore, domainEvents, returnToSender, retryManager, TestRetryMetrics.Create(fakeTime), new Lazy<IMessageDispatcher>(() => sender), new RecordingMessageActionAuditLog(), NullLogger<RetryProcessor>.Instance);

            bool c;
            do
            {
                try
                {
                    c = await processor.ProcessBatches();
                }
                catch (Exception)
                {
                    //Continue trying until there is no exception -> poison message is removed from the batch
                    c = true;
                }
            }
            while (c);

            var status = retryManager.GetStatusForRetryOperation("Test-group", RetryType.FailureGroup);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(status.RetryState, Is.EqualTo(RetryState.Completed));
                Assert.That(status.NumberOfMessagesPrepared, Is.EqualTo(3));
                Assert.That(status.NumberOfMessagesForwarded, Is.EqualTo(2));
                Assert.That(status.NumberOfMessagesSkipped, Is.EqualTo(1));
            }
        }

        [Test]
        public async Task When_a_group_has_one_batch_out_of_two_forwarded_the_status_is_Forwarding()
        {
            var domainEvents = new FakeDomainEvents();
            var retryManager = new RetryingManager(domainEvents, TestRetryMetrics.Create(fakeTime), NullLogger<RetryingManager>.Instance, fakeTime);

            await CreateAFailedMessageAndMarkAsPartOfRetryBatch(retryManager, "Test-group", true, 1001);

            var returnToSender = new ReturnToSender(BodyStorage, NullLogger<ReturnToSender>.Instance);

            var sender = new TestSender();

            var processor = new RetryProcessor(RetryStagingStore, MessageRedirectsDataStore, domainEvents, new TestReturnToSenderDequeuer(returnToSender, FailedMessageLifecycleStore, domainEvents, "TestEndpoint", new ErrorQueueNameCache(), new TestTransportCustomization()), retryManager, TestRetryMetrics.Create(fakeTime), new Lazy<IMessageDispatcher>(() => sender), new RecordingMessageActionAuditLog(), NullLogger<RetryProcessor>.Instance);

            await CompleteDatabaseOperation();

            await processor.ProcessBatches(); // mark ready
            await processor.ProcessBatches();

            var status = retryManager.GetStatusForRetryOperation("Test-group", RetryType.FailureGroup);
            Assert.That(status.RetryState, Is.EqualTo(RetryState.Forwarding));
        }

        [Test]
        public async Task When_a_selection_is_staged_each_message_is_audited_as_a_batch()
        {
            var domainEvents = new FakeDomainEvents();
            var retryManager = new RetryingManager(domainEvents, TestRetryMetrics.Create(fakeTime), NullLogger<RetryingManager>.Instance, fakeTime);
            var user = new AuditUser("alice-sub", "Alice");
            const string operationId = "op-sel";
            var ids = new[] { Guid.NewGuid().ToString(), Guid.NewGuid().ToString() };

            var messages = ids.Select(id => new FailedMessage
            {
                Id = PersistenceTestsContext.GenerateFailedMessageRecordId(id),
                UniqueMessageId = id,
                Status = FailedMessageStatus.Unresolved,
                ProcessingAttempts =
                [
                    new FailedMessage.ProcessingAttempt
                    {
                        AttemptedAt = DateTime.UtcNow,
                        MessageMetadata = [],
                        FailureDetails = new FailureDetails { AddressOfFailingEndpoint = "TestEndpoint" },
                        Headers = []
                    }
                ]
            }).ToArray();

            await PersistenceTestsContext.InsertFailedMessages(messages);
            await CompleteDatabaseOperation();

            var gateway = new CustomRetriesGateway(true, RetryBatchStore, retryManager, fakeTime);
            await gateway.StartRetryForMessageSelection(ids, user, operationId);
            await CompleteDatabaseOperation();

            var audit = new RecordingMessageActionAuditLog();
            var sender = new TestSender();
            var returnToSender = new TestReturnToSenderDequeuer(new ReturnToSender(BodyStorage, NullLogger<ReturnToSender>.Instance), FailedMessageLifecycleStore, domainEvents, "TestEndpoint", new ErrorQueueNameCache(), new TestTransportCustomization());
            var processor = new RetryProcessor(RetryStagingStore, MessageRedirectsDataStore, domainEvents, returnToSender, retryManager, TestRetryMetrics.Create(fakeTime), new Lazy<IMessageDispatcher>(() => sender), audit, NullLogger<RetryProcessor>.Instance);

            await processor.ProcessBatches(); // stage
            await processor.ProcessBatches(); // forward

            Assert.That(audit.Messages.Select(m => m.MessageId), Is.EquivalentTo(ids));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(audit.Messages, Has.All.Matches<RecordingMessageActionAuditLog.MessageEntry>(m => m.OperationId == operationId));
                Assert.That(audit.Messages, Has.All.Matches<RecordingMessageActionAuditLog.MessageEntry>(m => m.Kind == MessageActionKind.Retry));
                Assert.That(audit.Messages, Has.All.Matches<RecordingMessageActionAuditLog.MessageEntry>(m => m.Scope == MessageActionScope.Batch));
            }
        }

        [Test]
        public async Task When_a_group_is_staged_each_message_is_audited_with_the_initiating_user()
        {
            var domainEvents = new FakeDomainEvents();
            var retryManager = new RetryingManager(domainEvents, TestRetryMetrics.Create(fakeTime), NullLogger<RetryingManager>.Instance, fakeTime);
            var user = new AuditUser("alice-sub", "Alice");
            const string operationId = "op-abc";

            var ids = new[] { Guid.NewGuid().ToString(), Guid.NewGuid().ToString() };

            await CreateAFailedMessageAndMarkAsPartOfRetryBatch(retryManager, "Test-group", true, user, operationId, ids);

            var audit = new RecordingMessageActionAuditLog();
            var sender = new TestSender();
            var returnToSender = new TestReturnToSenderDequeuer(new ReturnToSender(BodyStorage, NullLogger<ReturnToSender>.Instance), FailedMessageLifecycleStore, domainEvents, "TestEndpoint", new ErrorQueueNameCache(), new TestTransportCustomization());
            var processor = new RetryProcessor(RetryStagingStore, MessageRedirectsDataStore, domainEvents, returnToSender, retryManager, TestRetryMetrics.Create(fakeTime), new Lazy<IMessageDispatcher>(() => sender), audit, NullLogger<RetryProcessor>.Instance);

            await processor.ProcessBatches(); // stage (emits per-message audit)
            await processor.ProcessBatches(); // forward

            Assert.That(audit.Messages.Select(m => m.MessageId), Is.EquivalentTo(ids));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(audit.Messages, Has.All.Matches<RecordingMessageActionAuditLog.MessageEntry>(m => m.User.Equals(user)));
                Assert.That(audit.Messages, Has.All.Matches<RecordingMessageActionAuditLog.MessageEntry>(m => m.OperationId == operationId));
                Assert.That(audit.Messages, Has.All.Matches<RecordingMessageActionAuditLog.MessageEntry>(m => m.Kind == MessageActionKind.Retry));
                Assert.That(audit.Messages, Has.All.Matches<RecordingMessageActionAuditLog.MessageEntry>(m => m.Scope == MessageActionScope.Group));
            }
        }

        // Claims messages that have no failed message behind them, which is what a batch looks like once
        // every message it covered has been claimed by an earlier batch or has aged out of retention.
        async Task<string> StageBatchWithoutMessages()
        {
            string[] messageIds = [Guid.NewGuid().ToString()];

            var batchId = await RetryBatchStore.CreateBatch(RetryDocumentManager.RetrySessionId, "Test-group", RetryType.FailureGroup, messageIds, "Test-group", DateTime.UtcNow);

            await RetryBatchStore.AssignMessagesToBatch(batchId, messageIds);
            await RetryBatchStore.MoveBatchToStaging(batchId);
            await CompleteDatabaseOperation();

            return batchId;
        }

        RetryProcessor CreateProcessor(IDomainEvents domainEvents, TestSender sender) =>
            new(RetryStagingStore,
                MessageRedirectsDataStore,
                domainEvents,
                new TestReturnToSenderDequeuer(new ReturnToSender(BodyStorage, NullLogger<ReturnToSender>.Instance), FailedMessageLifecycleStore, domainEvents, "TestEndpoint", new ErrorQueueNameCache(), new TestTransportCustomization()),
                new RetryingManager(domainEvents, TestRetryMetrics.Create(fakeTime), NullLogger<RetryingManager>.Instance, fakeTime),
                TestRetryMetrics.Create(fakeTime), new Lazy<IMessageDispatcher>(() => sender),
                new RecordingMessageActionAuditLog(),
                NullLogger<RetryProcessor>.Instance);

        Task CreateAFailedMessageAndMarkAsPartOfRetryBatch(RetryingManager retryManager, string groupId, bool progressToStaged, int numberOfMessages)
        {
            return CreateAFailedMessageAndMarkAsPartOfRetryBatch(retryManager, groupId, progressToStaged, Enumerable.Range(0, numberOfMessages).Select(i => Guid.NewGuid().ToString()).ToArray());
        }

        Task CreateAFailedMessageAndMarkAsPartOfRetryBatch(RetryingManager retryManager, string groupId, bool progressToStaged, params string[] messageIds) =>
            CreateAFailedMessageAndMarkAsPartOfRetryBatch(retryManager, groupId, progressToStaged, null, null, messageIds);

        Task CreateAFailedMessageAndMarkAsPartOfRetryBatch(RetryingManager retryManager, string groupId, bool progressToStaged, AuditUser? initiatedBy, string operationId, params string[] messageIds) =>
            CreateAFailedMessageAndMarkAsPartOfRetryBatch(retryManager, groupId, progressToStaged, initiatedBy, operationId, DateTime.UtcNow, messageIds);

        async Task CreateAFailedMessageAndMarkAsPartOfRetryBatch(RetryingManager retryManager, string groupId, bool progressToStaged, AuditUser? initiatedBy, string operationId, DateTime startTime, params string[] messageIds)
        {
            await InsertUnresolvedFailedMessages(groupId, messageIds);

            var gateway = new CustomRetriesGateway(progressToStaged, RetryBatchStore, retryManager, fakeTime);

            gateway.EnqueueRetryForFailureGroup(new RetriesGateway.RetryForFailureGroup(groupId, "Test-Context", groupType: null, startTime, initiatedBy, operationId));

            await CompleteDatabaseOperation();

            await gateway.ProcessNextBulkRetry();

            // Wait for indexes to catch up
            await CompleteDatabaseOperation();
        }

        async Task InsertUnresolvedFailedMessages(string groupId, params string[] messageIds)
        {
            var messages = messageIds.Select(id => new FailedMessage
            {
                Id = PersistenceTestsContext.GenerateFailedMessageRecordId(id),
                UniqueMessageId = id,
                FailureGroups =
                [
                    new FailedMessage.FailureGroup
                    {
                        Id = groupId,
                        Title = groupId,
                        Type = groupId
                    }
                ],
                Status = FailedMessageStatus.Unresolved,
                ProcessingAttempts =
                [
                    new FailedMessage.ProcessingAttempt
                    {
                        AttemptedAt = DateTime.UtcNow,
                        MessageMetadata = [],
                        FailureDetails = new FailureDetails { AddressOfFailingEndpoint = "TestEndpoint" },
                        Headers = []
                    }
                ]
            }).ToArray();

            await PersistenceTestsContext.InsertFailedMessages(messages);

            // Needs index FailedMessages_ByGroup
            // Needs index FailedMessages_UniqueMessageIdAndTimeOfFailures
            await CompleteDatabaseOperation();
        }

        class CustomRetriesGateway : RetriesGateway
        {
            public CustomRetriesGateway(bool progressToStaged, IRetryBatchStore store, RetryingManager retryManager, TimeProvider timeProvider)
                : base(store, retryManager, TestRetryMetrics.Create(timeProvider), NullLogger<RetriesGateway>.Instance, timeProvider)
            {
                this.progressToStaged = progressToStaged;
            }

            protected override Task MoveBatchToStaging(string batchId, CancellationToken cancellationToken = default)
            {
                if (progressToStaged)
                {
                    return base.MoveBatchToStaging(batchId, cancellationToken);
                }

                return Task.CompletedTask;
            }

            bool progressToStaged;
        }

        class CustomRetryDocumentManager : RetryDocumentManager
        {
            public CustomRetryDocumentManager(bool progressToStaged, IRetryBatchStore retryStore, RetryingManager retryManager)
                : base(new FakeApplicationLifetime(), retryStore, retryManager, NullLogger<RetryDocumentManager>.Instance)
            {
                RetrySessionId = Guid.NewGuid().ToString();
                this.progressToStaged = progressToStaged;
            }

            public override Task MoveBatchToStaging(string batchId, CancellationToken cancellationToken = default)
            {
                if (progressToStaged)
                {
                    return base.MoveBatchToStaging(batchId, cancellationToken);
                }

                return Task.CompletedTask;
            }

            bool progressToStaged;
        }


        class FakeApplicationLifetime : IHostApplicationLifetime
        {
            public void StopApplication() => throw new NotImplementedException();

            public CancellationToken ApplicationStarted { get; } = new CancellationToken();
            public CancellationToken ApplicationStopping { get; } = new CancellationToken();
            public CancellationToken ApplicationStopped { get; } = new CancellationToken();
        }

        class TestReturnToSenderDequeuer : ReturnToSenderDequeuer
        {
            public TestReturnToSenderDequeuer(ReturnToSender returnToSender, IFailedMessageLifecycleDataStore store, IDomainEvents domainEvents, string endpointName,
                ErrorQueueNameCache cache, ITransportCustomization transportCustomization)
                : base(returnToSender, store, domainEvents, transportCustomization, null, new Settings { InstanceName = endpointName }, cache, NullLogger<ReturnToSenderDequeuer>.Instance)
            {
            }

            public override Task Run(string forwardingBatchId, Predicate<MessageContext> filter, int? expectedMessageCount, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }
        }

        public class TestTransportCustomization : ITransportCustomization
        {
            public TransportInfrastructure TransportInfrastructure { get; set; }

            public void AddTransportForAudit(IServiceCollection services, TransportSettings transportSettings) => throw new NotImplementedException();
            public void AddTransportForMonitoring(IServiceCollection services, TransportSettings transportSettings) => throw new NotImplementedException();
            public void AddTransportForPrimary(IServiceCollection services, TransportSettings transportSettings) => throw new NotImplementedException();

            public Task<TransportInfrastructure> CreateTransportInfrastructure(string name,
                TransportSettings transportSettings, OnMessage onMessage = null, OnError onError = null,
                Func<string, Exception, CancellationToken, Task> onCriticalError = null,
                NServiceBus.TransportTransactionMode preferredTransactionMode =
                    NServiceBus.TransportTransactionMode.ReceiveOnly,
                CancellationToken cancellationToken = default) => Task.FromResult(TransportInfrastructure);
            public void CustomizeAuditEndpoint(NServiceBus.EndpointConfiguration endpointConfiguration, TransportSettings transportSettings) => throw new NotImplementedException();
            public void CustomizeMonitoringEndpoint(NServiceBus.EndpointConfiguration endpointConfiguration, TransportSettings transportSettings) => throw new NotImplementedException();
            public void CustomizePrimaryEndpoint(NServiceBus.EndpointConfiguration endpointConfiguration, TransportSettings transportSettings) => throw new NotImplementedException();
            public Task ProvisionQueues(TransportSettings transportSettings, IEnumerable<string> additionalQueues, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public string ToTransportQualifiedQueueName(string queueName) => queueName;
        }

        public class TestSender : IMessageDispatcher
        {
            public Action<UnicastTransportOperation> Callback { get; set; } = m => { };

            public Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction, CancellationToken cancellationToken = default)
            {
                foreach (var operation in outgoingMessages.UnicastTransportOperations)
                {
                    Callback(operation);
                }

                return Task.CompletedTask;
            }
        }

        public class TestTransportInfrastructure : TransportInfrastructure
        {
            public TestTransportInfrastructure(IReadOnlyDictionary<string, IMessageReceiver> receivers = null) => Receivers = receivers ?? new Dictionary<string, IMessageReceiver>();

            public string TransportAddress { get; set; }

            public override Task Shutdown(CancellationToken cancellationToken = new CancellationToken()) => throw new NotImplementedException();

            public override string ToTransportAddress(QueueAddress address) => TransportAddress;
        }
    }
}