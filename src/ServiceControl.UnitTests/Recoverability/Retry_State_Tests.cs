namespace ServiceControl.UnitTests.Recoverability
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Contracts.Operations;
    using MessageFailures;
    using NServiceBus.Extensibility;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using Operations;
    using Raven.Client.Documents;
    using Raven.TestDriver;
    using ServiceControl.Infrastructure;
    using ServiceControl.Infrastructure.DomainEvents;
    using ServiceControl.Recoverability;

    [TestFixture]
    public class Retry_State_Tests : RavenTestDriver
    {
        protected override void PreInitialize(IDocumentStore documentStore)
        {
            base.PreInitialize(documentStore);
            documentStore.Conventions.SaveEnumsAsIntegers = true;
        }

        [Test]
        public async Task When_a_group_is_processed_it_is_set_to_the_Preparing_state()
        {
            var domainEvents = new FakeDomainEvents();
            var retryManager = new RetryingManager(domainEvents);

            using (var documentStore = GetDocumentStore())
            {
                await CreateAFailedMessageAndMarkAsPartOfRetryBatch(documentStore, retryManager, "Test-group", true, 1);
                var status = retryManager.GetStatusForRetryOperation("Test-group", RetryType.FailureGroup);

                Assert.AreEqual(RetryState.Preparing, status.RetryState);
            }
        }

        [Test]
        public async Task When_a_group_is_prepared_and_SC_is_started_the_group_is_marked_as_failed()
        {
            var domainEvents = new FakeDomainEvents();
            var retryManager = new RetryingManager(domainEvents);

            using (var documentStore = GetDocumentStore())
            {
                await CreateAFailedMessageAndMarkAsPartOfRetryBatch(documentStore, retryManager, "Test-group", false, 1);

                new RetryBatches_ByStatusAndSession().Execute(documentStore);
                new FailedMessageRetries_ByBatch().Execute(documentStore);

                WaitForIndexing(documentStore);

                var documentManager = new CustomRetryDocumentManager(false, documentStore)
                {
                    OperationManager = retryManager
                };

                var orphanage = new FailedMessageRetries.AdoptOrphanBatchesFromPreviousSession(documentManager, documentStore);
                await orphanage.AdoptOrphanedBatchesAsync();

                var status = retryManager.GetStatusForRetryOperation("Test-group", RetryType.FailureGroup);
                Assert.True(status.Failed);
            }
        }

        [Test]
        public async Task When_a_group_is_prepared_and_SC_is_restarted_while_the_first_group_is_being_forwarded_then_the_count_still_matches()
        {
            var domainEvents = new FakeDomainEvents();
            var retryManager = new RetryingManager(domainEvents);

            using (var documentStore = GetDocumentStore())
            {
                await CreateAFailedMessageAndMarkAsPartOfRetryBatch(documentStore, retryManager, "Test-group", true, 2001);

                new RetryBatches_ByStatus_ReduceInitialBatchSize().Execute(documentStore);

                var sender = new TestSender();

                var processor = new RetryProcessor(documentStore, sender, domainEvents, new TestReturnToSenderDequeuer(new ReturnToSender(documentStore), documentStore, domainEvents, "TestEndpoint"), retryManager, TimeSpan.FromDays(90));

                WaitForIndexing(documentStore);

                using (var session = documentStore.OpenAsyncSession())
                {
                    await processor.ProcessBatches(session, CancellationToken.None); // mark ready
                    await session.SaveChangesAsync();


                    // Simulate SC restart
                    retryManager = new RetryingManager(domainEvents);

                    var documentManager = new CustomRetryDocumentManager(false, documentStore)
                    {
                        OperationManager = retryManager
                    };
                    await documentManager.RebuildRetryOperationState(session);

                    processor = new RetryProcessor(documentStore, sender, domainEvents, new TestReturnToSenderDequeuer(new ReturnToSender(documentStore), documentStore, domainEvents, "TestEndpoint"), retryManager, TimeSpan.FromDays(90));

                    await processor.ProcessBatches(session, CancellationToken.None);
                    await session.SaveChangesAsync();
                }

                var status = retryManager.GetStatusForRetryOperation("Test-group", RetryType.FailureGroup);
                Assert.AreEqual(2001, status.TotalNumberOfMessages);
            }
        }

        [Test]
        public async Task When_a_group_is_forwarded_the_status_is_Completed()
        {
            var domainEvents = new FakeDomainEvents();
            var retryManager = new RetryingManager(domainEvents);

            using (var documentStore = GetDocumentStore())
            {
                await CreateAFailedMessageAndMarkAsPartOfRetryBatch(documentStore, retryManager, "Test-group", true, 1);

                var sender = new TestSender();

                var returnToSender = new TestReturnToSenderDequeuer(new ReturnToSender(documentStore), documentStore, domainEvents, "TestEndpoint");
                var processor = new RetryProcessor(documentStore, sender, domainEvents, returnToSender, retryManager, TimeSpan.FromDays(90));

                using (var session = documentStore.OpenAsyncSession())
                {
                    await processor.ProcessBatches(session, CancellationToken.None); // mark ready
                    await session.SaveChangesAsync();

                    await processor.ProcessBatches(session, CancellationToken.None);
                    await session.SaveChangesAsync();
                }

                var status = retryManager.GetStatusForRetryOperation("Test-group", RetryType.FailureGroup);
                Assert.AreEqual(RetryState.Completed, status.RetryState);
            }
        }

        [Test]
        public async Task When_there_is_one_poison_message_it_is_removed_from_batch_and_the_status_is_Complete()
        {
            var domainEvents = new FakeDomainEvents();
            var retryManager = new RetryingManager(domainEvents);

            using (var documentStore = GetDocumentStore())
            {
                await CreateAFailedMessageAndMarkAsPartOfRetryBatch(documentStore, retryManager, "Test-group", true, "A", "B", "C");

                var sender = new TestSender();
                sender.Callback = operation =>
                {
                    //Always fails staging message B
                    if (operation.Message.MessageId == "FailedMessages/B")
                    {
                        throw new Exception("Simulated");
                    }
                };

                var returnToSender = new TestReturnToSenderDequeuer(new ReturnToSender(documentStore), documentStore, domainEvents, "TestEndpoint");
                var processor = new RetryProcessor(documentStore, sender, domainEvents, returnToSender, retryManager, TimeSpan.FromDays(90));

                bool c;
                do
                {
                    try
                    {
                        using (var session = documentStore.OpenAsyncSession())
                        {
                            c = await processor.ProcessBatches(session, CancellationToken.None);
                            await session.SaveChangesAsync();
                        }
                    }
                    catch (Exception)
                    {
                        //Continue trying until there is no exception -> poison message is removed from the batch
                        c = true;
                    }
                }
                while (c);

                var status = retryManager.GetStatusForRetryOperation("Test-group", RetryType.FailureGroup);

                Assert.AreEqual(RetryState.Completed, status.RetryState);
                Assert.AreEqual(3, status.NumberOfMessagesPrepared);
                Assert.AreEqual(2, status.NumberOfMessagesForwarded);
                Assert.AreEqual(1, status.NumberOfMessagesSkipped);
            }
        }

        [Test]
        public async Task When_a_group_has_one_batch_out_of_two_forwarded_the_status_is_Forwarding()
        {
            var domainEvents = new FakeDomainEvents();
            var retryManager = new RetryingManager(domainEvents);

            using (var documentStore = GetDocumentStore())
            {
                await CreateAFailedMessageAndMarkAsPartOfRetryBatch(documentStore, retryManager, "Test-group", true, 1001);

                var returnToSender = new ReturnToSender(documentStore);

                var sender = new TestSender();

                var processor = new RetryProcessor(documentStore, sender, domainEvents, new TestReturnToSenderDequeuer(returnToSender, documentStore, domainEvents, "TestEndpoint"), retryManager, TimeSpan.FromDays(90));

                WaitForIndexing(documentStore);

                using (var session = documentStore.OpenAsyncSession())
                {
                    await processor.ProcessBatches(session, CancellationToken.None); // mark ready
                    await session.SaveChangesAsync();

                    await processor.ProcessBatches(session, CancellationToken.None);
                    await session.SaveChangesAsync();
                }

                var status = retryManager.GetStatusForRetryOperation("Test-group", RetryType.FailureGroup);
                Assert.AreEqual(RetryState.Forwarding, status.RetryState);
            }
        }

        Task CreateAFailedMessageAndMarkAsPartOfRetryBatch(IDocumentStore documentStore, RetryingManager retryManager, string groupId, bool progressToStaged, int numberOfMessages)
        {
            return CreateAFailedMessageAndMarkAsPartOfRetryBatch(documentStore, retryManager, groupId, progressToStaged, Enumerable.Range(0, numberOfMessages).Select(i => Guid.NewGuid().ToString()).ToArray());
        }

        async Task CreateAFailedMessageAndMarkAsPartOfRetryBatch(IDocumentStore documentStore, RetryingManager retryManager, string groupId, bool progressToStaged, params string[] messageIds)
        {
            var messages = messageIds.Select(id => new FailedMessage
            {
                Id = FailedMessage.MakeDocumentId(id),
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
            });

            using (var session = documentStore.OpenAsyncSession())
            {
                foreach (var message in messages)
                {
                    await session.StoreAsync(message);
                }

                await session.SaveChangesAsync();
            }

            await new FailedMessages_ByGroup().ExecuteAsync(documentStore);
            // TODO: RAVEN5 - No transformers
            //await new FailedMessages_UniqueMessageIdAndTimeOfFailures().ExecuteAsync(documentStore);
            WaitForIndexing(documentStore);

            var documentManager = new CustomRetryDocumentManager(progressToStaged, documentStore);
            var gateway = new RetriesGateway(documentStore, documentManager);

            documentManager.OperationManager = retryManager;

            gateway.OperationManager = retryManager;

            gateway.StartRetryForIndex<FailureGroupMessageView, FailedMessages_ByGroup>("Test-group", RetryType.FailureGroup, DateTime.UtcNow, x => x.FailureGroupId == "Test-group", "Test-Context");

            WaitForIndexing(documentStore);

            await gateway.ProcessNextBulkRetry();
        }
    }

    class CustomRetryDocumentManager : RetryDocumentManager
    {
        public CustomRetryDocumentManager(bool progressToStaged, IDocumentStore documentStore)
            : base(new ShutdownNotifier(), documentStore, TimeSpan.FromDays(90))
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

        private bool progressToStaged;
    }

    class TestReturnToSenderDequeuer : ReturnToSenderDequeuer
    {
        public TestReturnToSenderDequeuer(ReturnToSender returnToSender, IDocumentStore store, IDomainEvents domainEvents, string endpointName)
            : base(returnToSender, store, domainEvents, endpointName, null /* rawEndpointFactory */)
        {
        }

        public override Task Run(string forwardingBatchId, Predicate<MessageContext> filter, CancellationToken cancellationToken, int? expectedMessageCount = null)
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