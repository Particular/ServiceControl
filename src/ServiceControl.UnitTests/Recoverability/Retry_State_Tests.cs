﻿using NServiceBus.Transports;
using NUnit.Framework;
using Raven.Client;
using ServiceControl.Contracts.Operations;
using ServiceControl.Infrastructure;
using ServiceControl.MessageFailures;
using ServiceControl.Operations.BodyStorage.RavenAttachments;
using ServiceControl.Recoverability;
using ServiceControl.UnitTests.Operations;
using System;
using System.Collections.Generic;
using System.Linq;
using NServiceBus;
using NServiceBus.Unicast;
using NServiceBus.ObjectBuilder.Common;
using NServiceBus.Unicast.Transport;

namespace ServiceControl.UnitTests.Recoverability
{
    [TestFixture]
    public class Retry_State_Tests
    {
        [Test]
        public void When_a_group_is_processed_it_is_set_to_the_Preparing_state()
        {
            var retryManager = new RetryOperationManager(new TestNotifier());

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
            var retryManager = new RetryOperationManager(new TestNotifier());

            using (var documentStore = InMemoryStoreBuilder.GetInMemoryStore())
            {
                CreateAFailedMessageAndMarkAsPartOfRetryBatch(documentStore, retryManager, "Test-group", false, 1);

                new RetryBatches_ByStatusAndSession().Execute(documentStore);
                new FailedMessageRetries_ByBatch().Execute(documentStore);

                documentStore.WaitForIndexing();

                var documentManager = new CustomRetryDocumentManager(false, documentStore)
                {
                    RetryOperationManager = retryManager
                };

                var orphanage = new FailedMessageRetries.AdoptOrphanBatchesFromPreviousSession(documentManager, null, documentStore);
                orphanage.AdoptOrphanedBatches();

                var status = retryManager.GetStatusForRetryOperation("Test-group", RetryType.FailureGroup);
                Assert.True(status.Failed);
            }
        }

        [Test]
        public void When_a_group_is_prepared_with_three_batches_and_SC_is_restarted_while_the_first_group_is_being_forwarded_then_the_count_still_matches()
        {
            var retryManager = new RetryOperationManager(new TestNotifier());

            using (var documentStore = InMemoryStoreBuilder.GetInMemoryStore())
            {
                CreateAFailedMessageAndMarkAsPartOfRetryBatch(documentStore, retryManager, "Test-group", true, 2001);

                new RetryBatches_ByStatus_ReduceInitialBatchSize().Execute(documentStore);

                var bodyStorage = new RavenAttachmentsBodyStorage
                {
                    DocumentStore = documentStore
                };

                var testBus = new TestBus();

                var sender = new TestSender();

                var settingsHolder = new NServiceBus.Settings.SettingsHolder();
                settingsHolder.Set("EndpointName", "TestEndpoint");

                var configure = new Configure(settingsHolder, new TestContainer(), new List<Action<NServiceBus.ObjectBuilder.IConfigureComponents>>(), new NServiceBus.Pipeline.PipelineSettings(new BusConfiguration()));

                var processor = new RetryProcessor(bodyStorage, sender, testBus, new TestReturnToSenderDequeuer(sender, documentStore, testBus, configure), retryManager);

                documentStore.WaitForIndexing();

                using (var session = documentStore.OpenSession())
                {
                    processor.ProcessBatches(session); // mark ready
                    session.SaveChanges();


                    // Simulate SC restart
                    retryManager = new RetryOperationManager(new TestNotifier());

                    var documentManager = new CustomRetryDocumentManager(false, documentStore)
                    {
                        RetryOperationManager = retryManager
                    };
                    documentManager.RebuildRetryOperationState(session);

                    processor = new RetryProcessor(bodyStorage, sender, testBus, new TestReturnToSenderDequeuer(sender, documentStore, testBus, configure), retryManager);

                    processor.ProcessBatches(session);
                    session.SaveChanges();
                }

                var status = retryManager.GetStatusForRetryOperation("Test-group", RetryType.FailureGroup);
                Assert.AreEqual(2001, status.TotalNumberOfMessages);
            }
        }

        [Test]
        public void When_a_group_is_forwarded_the_status_is_Completed()
        {
            var retryManager = new RetryOperationManager(new TestNotifier());

            using (var documentStore = InMemoryStoreBuilder.GetInMemoryStore())
            {
                CreateAFailedMessageAndMarkAsPartOfRetryBatch(documentStore, retryManager, "Test-group", true, 1);

                var bodyStorage = new RavenAttachmentsBodyStorage
                {
                    DocumentStore = documentStore
                };

                var testBus = new TestBus();

                var sender = new TestSender();

                var settingsHolder = new NServiceBus.Settings.SettingsHolder();
                settingsHolder.Set("EndpointName", "TestEndpoint");

                var configure = new Configure(settingsHolder, new TestContainer(), new List<Action<NServiceBus.ObjectBuilder.IConfigureComponents>>(), new NServiceBus.Pipeline.PipelineSettings(new BusConfiguration()));
                var returnToSender = new TestReturnToSenderDequeuer(sender, documentStore, testBus, configure);
                var processor = new RetryProcessor(bodyStorage, sender, testBus, returnToSender, retryManager);

                using (var session = documentStore.OpenSession())
                {
                    processor.ProcessBatches(session); // mark ready
                    session.SaveChanges();

                    processor.ProcessBatches(session);
                    session.SaveChanges();
                }

                var status = retryManager.GetStatusForRetryOperation("Test-group", RetryType.FailureGroup);
                Assert.AreEqual(RetryState.Completed, status.RetryState);
            }
        }

        [Test]
        public void When_a_group_has_one_batch_out_of_two_forwarded_the_status_is_Forwarding()
        {
            var retryManager = new RetryOperationManager(new TestNotifier());

            using (var documentStore = InMemoryStoreBuilder.GetInMemoryStore())
            {
                CreateAFailedMessageAndMarkAsPartOfRetryBatch(documentStore, retryManager, "Test-group", true, 1001);

                var bodyStorage = new RavenAttachmentsBodyStorage
                {
                    DocumentStore = documentStore
                };

                var testBus = new TestBus();

                var sender = new TestSender();

                var settingsHolder = new NServiceBus.Settings.SettingsHolder();
                settingsHolder.Set("EndpointName", "TestEndpoint");

                var configure = new Configure(settingsHolder, new TestContainer(), new List<Action<NServiceBus.ObjectBuilder.IConfigureComponents>>(), new NServiceBus.Pipeline.PipelineSettings(new BusConfiguration()));

                var processor = new RetryProcessor(bodyStorage, sender, testBus, new TestReturnToSenderDequeuer(sender, documentStore, testBus, configure), retryManager);

                documentStore.WaitForIndexing();

                using (var session = documentStore.OpenSession())
                {
                    processor.ProcessBatches(session); // mark ready
                    session.SaveChanges();

                    processor.ProcessBatches(session);
                    session.SaveChanges();
                }

                var status = retryManager.GetStatusForRetryOperation("Test-group", RetryType.FailureGroup);
                Assert.AreEqual(RetryState.Forwarding, status.RetryState);
            }
        }

        void CreateAFailedMessageAndMarkAsPartOfRetryBatch(IDocumentStore documentStore, RetryOperationManager retryManager, string groupId, bool progressToStaged, int numberOfMessages)
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

            documentManager.RetryOperationManager = retryManager;

            gateway.RetryOperationManager = retryManager;

            gateway.StartRetryForIndex<FailureGroupMessageView, FailedMessages_ByGroup>("Test-group", RetryType.FailureGroup, x => x.FailureGroupId == "Test-group", "Test-Context");

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
        public TestReturnToSenderDequeuer(ISendMessages sender, IDocumentStore store, IBus bus, Configure configure)
            : base(sender, store, bus, configure)
        {
        }

        public override void Run(Predicate<TransportMessage> filter, int? expectedMessageCount = default(int?))
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

        public IInMemoryOperations InMemory
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