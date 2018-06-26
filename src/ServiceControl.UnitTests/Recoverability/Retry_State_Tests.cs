namespace ServiceControl.UnitTests.Recoverability
{
    using NServiceBus.Extensibility;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using Raven.Client;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.Infrastructure;
    using ServiceControl.Infrastructure.DomainEvents;
    using ServiceControl.MessageFailures;
    using ServiceControl.Operations.BodyStorage.RavenAttachments;
    using ServiceControl.Recoverability;
    using ServiceControl.UnitTests.Operations;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    [TestFixture]
    public class Retry_State_Tests
    {
        [Test]
        public async Task When_a_group_is_processed_it_is_set_to_the_Preparing_state()
        {
            var domainEvents = new FakeDomainEvents();
            var retryManager = new RetryingManager(domainEvents);
            RetryingManager.RetryOperations = new Dictionary<string, InMemoryRetry>();

            using (var documentStore = InMemoryStoreBuilder.GetInMemoryStore())
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
            RetryingManager.RetryOperations = new Dictionary<string, InMemoryRetry>();

            using (var documentStore = InMemoryStoreBuilder.GetInMemoryStore())
            {
                await CreateAFailedMessageAndMarkAsPartOfRetryBatch(documentStore, retryManager, "Test-group", false, 1);

                new RetryBatches_ByStatusAndSession().Execute(documentStore);
                new FailedMessageRetries_ByBatch().Execute(documentStore);

                documentStore.WaitForIndexing();

                var documentManager = new CustomRetryDocumentManager(false, documentStore)
                {
                    OperationManager = retryManager
                };

                var orphanage = new FailedMessageRetries.AdoptOrphanBatchesFromPreviousSession(documentManager, null, documentStore);
                await orphanage.AdoptOrphanedBatchesAsync();

                var status = retryManager.GetStatusForRetryOperation("Test-group", RetryType.FailureGroup);
                Assert.True(status.Failed);
            }
        }

        [Test]
        public async Task When_a_group_is_prepared_with_three_batches_and_SC_is_restarted_while_the_first_group_is_being_forwarded_then_the_count_still_matches()
        {
            var domainEvents = new FakeDomainEvents();
            var retryManager = new RetryingManager(domainEvents);
            RetryingManager.RetryOperations = new Dictionary<string, InMemoryRetry>();

            using (var documentStore = InMemoryStoreBuilder.GetInMemoryStore())
            {
                await CreateAFailedMessageAndMarkAsPartOfRetryBatch(documentStore, retryManager, "Test-group", true, 2001);

                new RetryBatches_ByStatus_ReduceInitialBatchSize().Execute(documentStore);

                var sender = new TestSender();

                var bodyStorage = new RavenAttachmentsBodyStorage
                {
                    DocumentStore = documentStore
                };

                var processor = new RetryProcessor(sender, domainEvents, new TestReturnToSenderDequeuer(new ReturnToSender(bodyStorage), documentStore, domainEvents, "TestEndpoint"), retryManager);

                documentStore.WaitForIndexing();

                using (var session = documentStore.OpenAsyncSession())
                {
                    await processor.ProcessBatches(session, CancellationToken.None); // mark ready
                    await session.SaveChangesAsync();


                    // Simulate SC restart
                    retryManager = new RetryingManager(domainEvents);
                    RetryingManager.RetryOperations = new Dictionary<string, InMemoryRetry>();

                    var documentManager = new CustomRetryDocumentManager(false, documentStore)
                    {
                        OperationManager = retryManager
                    };
                    await documentManager.RebuildRetryOperationState(session);

                    processor = new RetryProcessor(sender, domainEvents, new TestReturnToSenderDequeuer(new ReturnToSender(bodyStorage), documentStore, domainEvents, "TestEndpoint"), retryManager);

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
            RetryingManager.RetryOperations = new Dictionary<string, InMemoryRetry>();

            using (var documentStore = InMemoryStoreBuilder.GetInMemoryStore())
            {
                await CreateAFailedMessageAndMarkAsPartOfRetryBatch(documentStore, retryManager, "Test-group", true, 1);

                var sender = new TestSender();

                var bodyStorage = new RavenAttachmentsBodyStorage
                {
                    DocumentStore = documentStore
                };

                var returnToSender = new TestReturnToSenderDequeuer(new ReturnToSender(bodyStorage), documentStore, domainEvents, "TestEndpoint");
                var processor = new RetryProcessor(sender, domainEvents, returnToSender, retryManager);

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
        public async Task When_a_group_has_one_batch_out_of_two_forwarded_the_status_is_Forwarding()
        {
            var domainEvents = new FakeDomainEvents();
            var retryManager = new RetryingManager(domainEvents);
            RetryingManager.RetryOperations = new Dictionary<string, InMemoryRetry>();

            using (var documentStore = InMemoryStoreBuilder.GetInMemoryStore())
            {
                await CreateAFailedMessageAndMarkAsPartOfRetryBatch(documentStore, retryManager, "Test-group", true, 1001);

                var bodyStorage = new RavenAttachmentsBodyStorage
                {
                    DocumentStore = documentStore
                };

                var returnToSender = new ReturnToSender(bodyStorage);

                var sender = new TestSender();

                var processor = new RetryProcessor(sender, domainEvents, new TestReturnToSenderDequeuer(returnToSender, documentStore, domainEvents, "TestEndpoint"), retryManager);

                documentStore.WaitForIndexing();

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

        async Task CreateAFailedMessageAndMarkAsPartOfRetryBatch(IDocumentStore documentStore, RetryingManager retryManager, string groupId, bool progressToStaged, int numberOfMessages)
        {
            var messages = Enumerable.Range(0, numberOfMessages).Select(i =>
            {
                var id = Guid.NewGuid().ToString();

                return new FailedMessage
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
                };
            });

            using (var session = documentStore.OpenAsyncSession())
            {
                foreach (var message in messages)
                {
                    await session.StoreAsync(message);
                }

                await session.SaveChangesAsync();
            }

            new FailedMessages_ByGroup().Execute(documentStore);

            documentStore.WaitForIndexing();

            var documentManager = new CustomRetryDocumentManager(progressToStaged, documentStore);
            var gateway = new RetriesGateway(documentStore, documentManager);

            documentManager.OperationManager = retryManager;

            gateway.OperationManager = retryManager;

            gateway.StartRetryForIndex<FailureGroupMessageView, FailedMessages_ByGroup>("Test-group", RetryType.FailureGroup, DateTime.UtcNow, x => x.FailureGroupId == "Test-group", "Test-Context");

            documentStore.WaitForIndexing();

            await gateway.ProcessNextBulkRetry();
        }
    }

    public class CustomRetryDocumentManager : RetryDocumentManager
    {
        private bool progressToStaged;

        public CustomRetryDocumentManager(bool progressToStaged, IDocumentStore documentStore)
            : base(new ShutdownNotifier(), documentStore)
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
    }

    public class TestReturnToSenderDequeuer : ReturnToSenderDequeuer
    {
        public TestReturnToSenderDequeuer(ReturnToSender returnToSender, IDocumentStore store, IDomainEvents domainEvents, string endpointName)
            : base(returnToSender, store, domainEvents, endpointName, null /* rawEndpointFactory */)
        {
        }

        public override Task Run(Predicate<MessageContext> filter, CancellationToken cancellationToken, int? expectedMessageCount = null)
        {
            return Task.FromResult(0);
        }
    }

    public class TestSender : IDispatchMessages
    {

        public Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction, ContextBag context)
        {
            return Task.FromResult(0);
        }
    }
}