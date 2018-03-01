using NServiceBus.Transports;
using NUnit.Framework;
using Raven.Client;
using ServiceControl.Contracts.Operations;
using ServiceControl.Infrastructure;
using ServiceControl.MessageFailures;
using ServiceControl.Operations.BodyStorage.RavenAttachments;
using ServiceControl.Recoverability;
using System;
using System.Collections.Generic;
using System.Linq;
using NServiceBus;
using NServiceBus.Unicast;
using NServiceBus.ObjectBuilder.Common;

namespace ServiceControl.UnitTests.Recoverability
{
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.Infrastructure.DomainEvents;
    using ServiceControl.Operations.BodyStorage;
    using ServiceControl.UnitTests.Operations;

    [TestFixture]
    public class Retry_State_Tests
    {
        [Test]
        public void When_a_group_is_processed_it_is_set_to_the_Preparing_state()
        {
            var domainEvents = new FakeDomainEvents();
            var retryManager = new RetryingManager(domainEvents);
            RetryingManager.RetryOperations = new Dictionary<string, InMemoryRetry>();

            using (var documentStore = InMemoryStoreBuilder.GetInMemoryStore())
            {
                CreateAFailedMessageAndMarkAsPartOfRetryBatch(documentStore, retryManager, "Test-group", true, 1);
                var status = retryManager.GetStatusForRetryOperation("Test-group", RetryType.FailureGroup);

                Assert.AreEqual(RetryState.Preparing, status.RetryState);
            }
        }

        [Test]
        public void When_a_group_is_prepared_and_SC_is_started_the_group_is_marked_as_failed()
        {
            var domainEvents = new FakeDomainEvents();
            var retryManager = new RetryingManager(domainEvents);
            RetryingManager.RetryOperations = new Dictionary<string, InMemoryRetry>();

            using (var documentStore = InMemoryStoreBuilder.GetInMemoryStore())
            {
                CreateAFailedMessageAndMarkAsPartOfRetryBatch(documentStore, retryManager, "Test-group", false, 1);

                new RetryBatches_ByStatusAndSession().Execute(documentStore);
                new FailedMessageRetries_ByBatch().Execute(documentStore);

                documentStore.WaitForIndexing();

                var documentManager = new CustomRetryDocumentManager(false, documentStore)
                {
                    OperationManager = retryManager
                };

                var orphanage = new FailedMessageRetries.AdoptOrphanBatchesFromPreviousSession(documentManager, null, documentStore);
                orphanage.AdoptOrphanedBatches();

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
                CreateAFailedMessageAndMarkAsPartOfRetryBatch(documentStore, retryManager, "Test-group", true, 2001);

                new RetryBatches_ByStatus_ReduceInitialBatchSize().Execute(documentStore);

                var sender = new TestSender();

                var bodyStorage = new RavenAttachmentsBodyStorage
                {
                    DocumentStore = documentStore
                };

                var settingsHolder = new NServiceBus.Settings.SettingsHolder();
                settingsHolder.Set("EndpointName", "TestEndpoint");

                var configure = new Configure(settingsHolder, new TestContainer(), new List<Action<NServiceBus.ObjectBuilder.IConfigureComponents>>(), new NServiceBus.Pipeline.PipelineSettings(new BusConfiguration()));

                var processor = new RetryProcessor(sender, domainEvents, new TestReturnToSenderDequeuer(bodyStorage, sender, documentStore, domainEvents, configure), retryManager);

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

                    processor = new RetryProcessor(sender, domainEvents, new TestReturnToSenderDequeuer(bodyStorage, sender, documentStore, domainEvents, configure), retryManager);

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
                CreateAFailedMessageAndMarkAsPartOfRetryBatch(documentStore, retryManager, "Test-group", true, 1);

                var sender = new TestSender();

                var bodyStorage = new RavenAttachmentsBodyStorage
                {
                    DocumentStore = documentStore
                };

                var settingsHolder = new NServiceBus.Settings.SettingsHolder();
                settingsHolder.Set("EndpointName", "TestEndpoint");

                var configure = new Configure(settingsHolder, new TestContainer(), new List<Action<NServiceBus.ObjectBuilder.IConfigureComponents>>(), new NServiceBus.Pipeline.PipelineSettings(new BusConfiguration()));
                var returnToSender = new TestReturnToSenderDequeuer(bodyStorage, sender, documentStore, domainEvents, configure);
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
                CreateAFailedMessageAndMarkAsPartOfRetryBatch(documentStore, retryManager, "Test-group", true, 1001);

                var bodyStorage = new RavenAttachmentsBodyStorage
                {
                    DocumentStore = documentStore
                };

                var sender = new TestSender();

                var settingsHolder = new NServiceBus.Settings.SettingsHolder();
                settingsHolder.Set("EndpointName", "TestEndpoint");

                var configure = new Configure(settingsHolder, new TestContainer(), new List<Action<NServiceBus.ObjectBuilder.IConfigureComponents>>(), new NServiceBus.Pipeline.PipelineSettings(new BusConfiguration()));

                var processor = new RetryProcessor(sender, domainEvents, new TestReturnToSenderDequeuer(bodyStorage, sender, documentStore, domainEvents, configure), retryManager);

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

        void CreateAFailedMessageAndMarkAsPartOfRetryBatch(IDocumentStore documentStore, RetryingManager retryManager, string groupId, bool progressToStaged, int numberOfMessages)
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

            using (var session = documentStore.OpenSession())
            {
                foreach (var message in messages)
                {
                    session.Store(message);
                }

                session.SaveChanges();
            }

            new FailedMessages_ByGroup().Execute(documentStore);

            documentStore.WaitForIndexing();

            var documentManager = new CustomRetryDocumentManager(progressToStaged, documentStore);
            var gateway = new RetriesGateway(documentStore, documentManager);

            documentManager.OperationManager = retryManager;

            gateway.OperationManager = retryManager;

            gateway.StartRetryForIndex<FailureGroupMessageView, FailedMessages_ByGroup>("Test-group", RetryType.FailureGroup, DateTime.UtcNow, x => x.FailureGroupId == "Test-group", "Test-Context");

            documentStore.WaitForIndexing();

            gateway.ProcessNextBulkRetry();
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

        public override void MoveBatchToStaging(string batchDocumentId, string[] failedMessageRetryIds)
        {
            if (progressToStaged)
            {
                base.MoveBatchToStaging(batchDocumentId, failedMessageRetryIds);
            }
        }
    }

    public class TestReturnToSenderDequeuer : ReturnToSenderDequeuer
    {
        public TestReturnToSenderDequeuer(IBodyStorage bodyStorage, ISendMessages sender, IDocumentStore store, IDomainEvents domainEvents, Configure configure)
            : base(bodyStorage, sender, store, domainEvents, configure)
        {
        }

        public override void Run(Predicate<TransportMessage> filter, CancellationToken token, int? expectedMessageCount = default(int?))
        {
            // NOOP
        }
    }

    public class TestContainer : IContainer
    {
        public object Build(Type typeToBuild)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<object> BuildAll(Type typeToBuild)
        {
            throw new NotImplementedException();
        }

        public IContainer BuildChildContainer()
        {
            throw new NotImplementedException();
        }

        public void Configure(Type component, DependencyLifecycle dependencyLifecycle)
        {
        }

        public void Configure<T>(Func<T> component, DependencyLifecycle dependencyLifecycle)
        {
        }

        public void ConfigureProperty(Type component, string property, object value)
        {
        }

        public void Dispose()
        {
        }

        public bool HasComponent(Type componentType)
        {
            throw new NotImplementedException();
        }

        public void RegisterSingleton(Type lookupType, object instance)
        {
        }

        public void Release(object instance)
        {
        }
    }

    public class TestSender : ISendMessages
    {
        public void Send(TransportMessage message, SendOptions sendOptions)
        {
        }
    }

    public class TestBus : IBus
    {
        public IMessageContext CurrentMessageContext
        {
            get
            {
                throw new NotImplementedException();
            }
        }

#pragma warning disable CS0618
        public IInMemoryOperations InMemory
#pragma warning restore CS0618
        {
        get
            {
                throw new NotImplementedException();
            }
        }

        public IDictionary<string, string> OutgoingHeaders
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public ICallback Defer(DateTime processAt, object message)
        {
            throw new NotImplementedException();
        }

        public ICallback Defer(TimeSpan delay, object message)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
        }

        public void DoNotContinueDispatchingCurrentMessageToHandlers()
        {
        }

        public void ForwardCurrentMessageTo(string destination)
        {
        }

        public void HandleCurrentMessageLater()
        {
        }

        public void Publish<T>()
        {
        }

        public void Publish<T>(Action<T> messageConstructor)
        {
        }

        public void Publish<T>(T message)
        {
        }

        public void Reply(object message)
        {
        }

        public void Reply<T>(Action<T> messageConstructor)
        {
        }

        public void Return<T>(T errorEnum)
        {
        }

        public ICallback Send(object message)
        {
            throw new NotImplementedException();
        }

        public ICallback Send(Address address, object message)
        {
            throw new NotImplementedException();
        }

        public ICallback Send(string destination, object message)
        {
            throw new NotImplementedException();
        }

        public ICallback Send(Address address, string correlationId, object message)
        {
            throw new NotImplementedException();
        }

        public ICallback Send(string destination, string correlationId, object message)
        {
            throw new NotImplementedException();
        }

        public ICallback Send<T>(Action<T> messageConstructor)
        {
            throw new NotImplementedException();
        }

        public ICallback Send<T>(Address address, Action<T> messageConstructor)
        {
            throw new NotImplementedException();
        }

        public ICallback Send<T>(string destination, Action<T> messageConstructor)
        {
            throw new NotImplementedException();
        }

        public ICallback Send<T>(Address address, string correlationId, Action<T> messageConstructor)
        {
            throw new NotImplementedException();
        }

        public ICallback Send<T>(string destination, string correlationId, Action<T> messageConstructor)
        {
            throw new NotImplementedException();
        }

        public ICallback SendLocal(object message)
        {
            throw new NotImplementedException();
        }

        public ICallback SendLocal<T>(Action<T> messageConstructor)
        {
            throw new NotImplementedException();
        }

        public void Subscribe(Type messageType)
        {
        }

        public void Subscribe<T>()
        {
        }

        public void Unsubscribe(Type messageType)
        {
        }

        public void Unsubscribe<T>()
        {
        }
    }
}