namespace ServiceControl.PersistenceTests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using NServiceBus.Extensibility;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.Infrastructure.BackgroundTasks;
    using ServiceControl.Infrastructure.DomainEvents;
    using ServiceControl.MessageFailures;
    using ServiceControl.Persistence;
    using ServiceControl.Recoverability;

    [NonParallelizable]
    class RetryStateTests : PersistenceTestBase
    {
        [Test]
        public async Task When_a_group_is_processed_it_is_set_to_the_Preparing_state()
        {
            var domainEvents = new FakeDomainEvents();
            var retryManager = new RetryingManager(domainEvents);

            await CreateAFailedMessageAndMarkAsPartOfRetryBatch(retryManager, "Test-group", true, 1);
            var status = retryManager.GetStatusForRetryOperation("Test-group", RetryType.FailureGroup);

            Assert.AreEqual(RetryState.Preparing, status.RetryState);
        }

        [Test]
        public async Task When_a_group_is_prepared_and_SC_is_started_the_group_is_marked_as_failed()
        {
            var domainEvents = new FakeDomainEvents();
            var retryManager = new RetryingManager(domainEvents);

            await CreateAFailedMessageAndMarkAsPartOfRetryBatch(retryManager, "Test-group", false, 1);

            var documentManager = new CustomRetryDocumentManager(false, RetryStore, retryManager);

            var orphanage = new RecoverabilityComponent.AdoptOrphanBatchesFromPreviousSessionHostedService(documentManager, new AsyncTimer());
            await orphanage.AdoptOrphanedBatchesAsync();
            CompleteDatabaseOperation();

            var status = retryManager.GetStatusForRetryOperation("Test-group", RetryType.FailureGroup);
            Assert.True(status.Failed);
        }

        [Test]
        public async Task When_a_group_is_prepared_with_three_batches_and_SC_is_restarted_while_the_first_group_is_being_forwarded_then_the_count_still_matches()
        {
            var domainEvents = new FakeDomainEvents();
            var retryManager = new RetryingManager(domainEvents);

            await CreateAFailedMessageAndMarkAsPartOfRetryBatch(retryManager, "Test-group", true, 2001);

            var sender = new TestSender();
            var processor = new RetryProcessor(RetryBatchesStore, domainEvents, new TestReturnToSenderDequeuer(new ReturnToSender(BodyStorage, ErrorStore), ErrorStore, domainEvents, "TestEndpoint"), retryManager, PersistenceSettings);

            // Needs index RetryBatches_ByStatus_ReduceInitialBatchSize
            CompleteDatabaseOperation();

            await processor.ProcessBatches(sender); // mark ready

            // Simulate SC restart
            retryManager = new RetryingManager(domainEvents);

            var documentManager = new CustomRetryDocumentManager(false, RetryStore, retryManager);

            await documentManager.RebuildRetryOperationState();

            processor = new RetryProcessor(RetryBatchesStore, domainEvents, new TestReturnToSenderDequeuer(new ReturnToSender(BodyStorage, ErrorStore), ErrorStore, domainEvents, "TestEndpoint"), retryManager, PersistenceSettings);

            await processor.ProcessBatches(sender);

            var status = retryManager.GetStatusForRetryOperation("Test-group", RetryType.FailureGroup);
            Assert.AreEqual(2001, status.TotalNumberOfMessages);
        }

        [Test]
        public async Task When_a_group_is_forwarded_the_status_is_Completed()
        {
            var domainEvents = new FakeDomainEvents();
            var retryManager = new RetryingManager(domainEvents);

            await CreateAFailedMessageAndMarkAsPartOfRetryBatch(retryManager, "Test-group", true, 1);

            var sender = new TestSender();

            var returnToSender = new TestReturnToSenderDequeuer(new ReturnToSender(BodyStorage, ErrorStore), ErrorStore, domainEvents, "TestEndpoint");
            var processor = new RetryProcessor(RetryBatchesStore, domainEvents, returnToSender, retryManager, PersistenceSettings);

            await processor.ProcessBatches(sender); // mark ready
            await processor.ProcessBatches(sender);

            var status = retryManager.GetStatusForRetryOperation("Test-group", RetryType.FailureGroup);
            Assert.AreEqual(RetryState.Completed, status.RetryState);
        }


        void TraceWriteFirstChanceException(object x, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs ea)
        {
            var text = ea.Exception.ToString().Replace("\n", "\n    | ");
            Trace.WriteLine("WARN| FirstChanceException: " + text);
        }

        [SetUp]
        public void Register() => AppDomain.CurrentDomain.FirstChanceException += TraceWriteFirstChanceException;

        [TearDown]
        public void Unregister() => AppDomain.CurrentDomain.FirstChanceException -= TraceWriteFirstChanceException;

        [Test]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        [TestCase(9)]
        [TestCase(10)]
        [TestCase(11)]
        [TestCase(12)]
        [TestCase(13)]
        [TestCase(14)]
        [TestCase(15)]
        [TestCase(16)]
        [TestCase(17)]
        [TestCase(18)]
        [TestCase(19)]
        [TestCase(20)]
        [TestCase(21)]
        [TestCase(22)]
        [TestCase(23)]
        [TestCase(24)]
        [TestCase(25)]
        [TestCase(26)]
        [TestCase(27)]
        [TestCase(28)]
        [TestCase(29)]
        [TestCase(30)]
        public async Task When_there_is_one_poison_message_it_is_removed_from_batch_and_the_status_is_Complete(int num)
        {
            TestContext.Out.WriteLine($"Heisenbug Test Number {num}");

            var domainEvents = new FakeDomainEvents();
            var retryManager = new RetryingManager(domainEvents);

            await CreateAFailedMessageAndMarkAsPartOfRetryBatch(retryManager, "Test-group", true, "A", "B", "C");

            var sender = new TestSender
            {
                Callback = operation =>
                {
                    //Always fails staging message B
                    if (operation.Message.MessageId == "FailedMessages/B")
                    {
                        throw new Exception("Simulated");
                    }
                }
            };

            var returnToSender = new TestReturnToSenderDequeuer(new ReturnToSender(BodyStorage, ErrorStore), ErrorStore, domainEvents, "TestEndpoint");
            var processor = new RetryProcessor(RetryBatchesStore, domainEvents, returnToSender, retryManager, PersistenceSettings);

            bool c;
            do
            {
                try
                {
                    c = await processor.ProcessBatches(sender);
                }
                catch (Exception ex)
                {
                    //Continue trying until there is no exception -> poison message is removed from the batch
                    Trace.WriteLine("WARN| ProcessBatches failed: " + ex);
                    await Task.Delay(100);
                    c = true;
                }
            }
            while (c);

            CompleteDatabaseOperation();

            var status = retryManager.GetStatusForRetryOperation("Test-group", RetryType.FailureGroup);

            Assert.AreEqual(RetryState.Completed, status.RetryState);
            Assert.AreEqual(3, status.NumberOfMessagesPrepared);
            Assert.AreEqual(2, status.NumberOfMessagesForwarded);
            Assert.AreEqual(1, status.NumberOfMessagesSkipped);
        }

        [Test]
        public async Task When_a_group_has_one_batch_out_of_two_forwarded_the_status_is_Forwarding()
        {
            var domainEvents = new FakeDomainEvents();
            var retryManager = new RetryingManager(domainEvents);

            await CreateAFailedMessageAndMarkAsPartOfRetryBatch(retryManager, "Test-group", true, 1001);

            var returnToSender = new ReturnToSender(BodyStorage, ErrorStore);

            var sender = new TestSender();

            var processor = new RetryProcessor(RetryBatchesStore, domainEvents, new TestReturnToSenderDequeuer(returnToSender, ErrorStore, domainEvents, "TestEndpoint"), retryManager, PersistenceSettings);

            CompleteDatabaseOperation();

            await processor.ProcessBatches(sender); // mark ready
            await processor.ProcessBatches(sender);

            var status = retryManager.GetStatusForRetryOperation("Test-group", RetryType.FailureGroup);
            Assert.AreEqual(RetryState.Forwarding, status.RetryState);
        }

        Task CreateAFailedMessageAndMarkAsPartOfRetryBatch(RetryingManager retryManager, string groupId, bool progressToStaged, int numberOfMessages)
        {
            return CreateAFailedMessageAndMarkAsPartOfRetryBatch(retryManager, groupId, progressToStaged, Enumerable.Range(0, numberOfMessages).Select(i => Guid.NewGuid().ToString()).ToArray());
        }

        async Task CreateAFailedMessageAndMarkAsPartOfRetryBatch(RetryingManager retryManager, string groupId, bool progressToStaged, params string[] messageIds)
        {
            var messages = messageIds.Select(id => new FailedMessage
            {
                Id = FailedMessageIdGenerator.MakeDocumentId(id),
                UniqueMessageId = id,
                FailureGroups = new List<FailedMessage.FailureGroup>
                {
                    new FailedMessage.FailureGroup
                    {
                        Id = groupId,
                        Title = groupId,
                        Type = groupId
                    }
                },
                Status = FailedMessageStatus.Unresolved,
                ProcessingAttempts = new List<FailedMessage.ProcessingAttempt>
                {
                    new FailedMessage.ProcessingAttempt
                    {
                        AttemptedAt = DateTime.UtcNow,
                        MessageMetadata = new Dictionary<string, object>(),
                        FailureDetails = new FailureDetails(),
                        Headers = new Dictionary<string, string>()
                    }
                }
            }).ToArray();

            await ErrorStore.StoreFailedMessagesForTestsOnly(messages);

            // Needs index FailedMessages_ByGroup
            // Needs index FailedMessages_UniqueMessageIdAndTimeOfFailures
            CompleteDatabaseOperation();

            var documentManager = new CustomRetryDocumentManager(progressToStaged, RetryStore, retryManager);
            var gateway = new CustomRetriesGateway(progressToStaged, RetryStore, retryManager);

            gateway.EnqueueRetryForFailureGroup(new RetriesGateway.RetryForFailureGroup(groupId, "Test-Context", groupType: null, DateTime.UtcNow));

            CompleteDatabaseOperation();

            await gateway.ProcessNextBulkRetry();

            // Wait for indexes to catch up
            CompleteDatabaseOperation();
        }

        class CustomRetriesGateway : RetriesGateway
        {
            public CustomRetriesGateway(bool progressToStaged, IRetryDocumentDataStore store, RetryingManager retryManager)
                : base(store, retryManager)
            {
                this.progressToStaged = progressToStaged;
            }

            protected override Task MoveBatchToStaging(string batchDocumentId)
            {
                if (progressToStaged)
                {
                    return base.MoveBatchToStaging(batchDocumentId);
                }

                return Task.FromResult(0);
            }

            bool progressToStaged;
        }

        class CustomRetryDocumentManager : RetryDocumentManager
        {
            public CustomRetryDocumentManager(bool progressToStaged, IRetryDocumentDataStore retryStore, RetryingManager retryManager)
                : base(new FakeApplicationLifetime(), retryStore, retryManager)
            {
                RetrySessionId = Guid.NewGuid().ToString();
                this.progressToStaged = progressToStaged;
            }

            public override Task MoveBatchToStaging(string batchDocumentId)
            {
                if (progressToStaged)
                {
                    return base.MoveBatchToStaging(batchDocumentId);
                }

                return Task.FromResult(0);
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
            public TestReturnToSenderDequeuer(ReturnToSender returnToSender, IErrorMessageDataStore store, IDomainEvents domainEvents, string endpointName)
                : base(returnToSender, store, domainEvents, null, new Settings(endpointName))
            {
            }

            public override Task Run(string forwardingBatchId, Predicate<MessageContext> filter, int? expectedMessageCount, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(0);
            }
        }

        public class TestSender : IDispatchMessages
        {
            public Action<UnicastTransportOperation> Callback { get; set; } = m => { };

            public Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction, ContextBag context)
            {
                foreach (var operation in outgoingMessages.UnicastTransportOperations)
                {
                    Callback(operation);
                }

                return Task.FromResult(0);
            }
        }
    }
}
