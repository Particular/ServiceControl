namespace ServiceControl.Persistence.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.Infrastructure.BackgroundTasks;
    using ServiceControl.Infrastructure.DomainEvents;
    using ServiceControl.MessageFailures;
    using ServiceControl.Persistence;
    using ServiceControl.Recoverability;
    using ServiceControl.Transports;
    using QueueAddress = NServiceBus.Transport.QueueAddress;

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

            Assert.That(status.RetryState, Is.EqualTo(RetryState.Preparing));
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

            var testReturnToSenderDequeuer = new TestReturnToSenderDequeuer(new ReturnToSender(ErrorStore), ErrorStore, domainEvents, "TestEndpoint",
                errorQueueNameCache, transportCustomization);

            await testReturnToSenderDequeuer.StartAsync(new CancellationToken());

            Assert.That(errorQueueNameCache.ResolvedErrorAddress, Is.EqualTo(transportInfrastructure.TransportAddress));
        }

        [Test]
        public async Task When_a_group_is_prepared_with_three_batches_and_SC_is_restarted_while_the_first_group_is_being_forwarded_then_the_count_still_matches()
        {
            var domainEvents = new FakeDomainEvents();
            var retryManager = new RetryingManager(domainEvents);

            await CreateAFailedMessageAndMarkAsPartOfRetryBatch(retryManager, "Test-group", true, 2001);

            var sender = new TestSender();
            var processor = new RetryProcessor(RetryBatchesStore, domainEvents, new TestReturnToSenderDequeuer(new ReturnToSender(ErrorStore), ErrorStore, domainEvents, "TestEndpoint", new ErrorQueueNameCache(), new TestTransportCustomization()), retryManager, new Lazy<IMessageDispatcher>(() => sender));

            // Needs index RetryBatches_ByStatus_ReduceInitialBatchSize
            CompleteDatabaseOperation();

            await processor.ProcessBatches(); // mark ready

            // Simulate SC restart
            retryManager = new RetryingManager(domainEvents);

            var documentManager = new CustomRetryDocumentManager(false, RetryStore, retryManager);

            await documentManager.RebuildRetryOperationState();

            processor = new RetryProcessor(RetryBatchesStore, domainEvents, new TestReturnToSenderDequeuer(new ReturnToSender(ErrorStore), ErrorStore, domainEvents, "TestEndpoint", new ErrorQueueNameCache(), new TestTransportCustomization()), retryManager, new Lazy<IMessageDispatcher>(() => sender));

            await processor.ProcessBatches();

            var status = retryManager.GetStatusForRetryOperation("Test-group", RetryType.FailureGroup);
            Assert.That(status.TotalNumberOfMessages, Is.EqualTo(2001));
        }

        [Test]
        public async Task When_a_group_is_forwarded_the_status_is_Completed()
        {
            var domainEvents = new FakeDomainEvents();
            var retryManager = new RetryingManager(domainEvents);

            await CreateAFailedMessageAndMarkAsPartOfRetryBatch(retryManager, "Test-group", true, 1);

            var sender = new TestSender();

            var returnToSender = new TestReturnToSenderDequeuer(new ReturnToSender(ErrorStore), ErrorStore, domainEvents, "TestEndpoint", new ErrorQueueNameCache(), new TestTransportCustomization());
            var processor = new RetryProcessor(RetryBatchesStore, domainEvents, returnToSender, retryManager, new Lazy<IMessageDispatcher>(() => sender));

            await processor.ProcessBatches(); // mark ready
            await processor.ProcessBatches();

            var status = retryManager.GetStatusForRetryOperation("Test-group", RetryType.FailureGroup);
            Assert.That(status.RetryState, Is.EqualTo(RetryState.Completed));
        }

        [Test]
        public async Task When_there_is_one_poison_message_it_is_removed_from_batch_and_the_status_is_Complete()
        {
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

            var returnToSender = new TestReturnToSenderDequeuer(new ReturnToSender(ErrorStore), ErrorStore, domainEvents, "TestEndpoint", new ErrorQueueNameCache(), new TestTransportCustomization());
            var processor = new RetryProcessor(RetryBatchesStore, domainEvents, returnToSender, retryManager, new Lazy<IMessageDispatcher>(() => sender));

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

            Assert.Multiple(() =>
            {
                Assert.That(status.RetryState, Is.EqualTo(RetryState.Completed));
                Assert.That(status.NumberOfMessagesPrepared, Is.EqualTo(3));
                Assert.That(status.NumberOfMessagesForwarded, Is.EqualTo(2));
                Assert.That(status.NumberOfMessagesSkipped, Is.EqualTo(1));
            });
        }

        [Test]
        public async Task When_a_group_has_one_batch_out_of_two_forwarded_the_status_is_Forwarding()
        {
            var domainEvents = new FakeDomainEvents();
            var retryManager = new RetryingManager(domainEvents);

            await CreateAFailedMessageAndMarkAsPartOfRetryBatch(retryManager, "Test-group", true, 1001);

            var returnToSender = new ReturnToSender(ErrorStore);

            var sender = new TestSender();

            var processor = new RetryProcessor(RetryBatchesStore, domainEvents, new TestReturnToSenderDequeuer(returnToSender, ErrorStore, domainEvents, "TestEndpoint", new ErrorQueueNameCache(), new TestTransportCustomization()), retryManager, new Lazy<IMessageDispatcher>(() => sender));

            CompleteDatabaseOperation();

            await processor.ProcessBatches(); // mark ready
            await processor.ProcessBatches();

            var status = retryManager.GetStatusForRetryOperation("Test-group", RetryType.FailureGroup);
            Assert.That(status.RetryState, Is.EqualTo(RetryState.Forwarding));
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
                        FailureDetails = new FailureDetails(),
                        Headers = []
                    }
                ]
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

                return Task.CompletedTask;
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
            public TestReturnToSenderDequeuer(ReturnToSender returnToSender, IErrorMessageDataStore store, IDomainEvents domainEvents, string endpointName,
                ErrorQueueNameCache cache, ITransportCustomization transportCustomization)
                : base(returnToSender, store, domainEvents, transportCustomization, null, new Settings { InstanceName = endpointName }, cache)
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
                Func<string, Exception, Task> onCriticalError = null,
                NServiceBus.TransportTransactionMode preferredTransactionMode =
                    NServiceBus.TransportTransactionMode.ReceiveOnly) => Task.FromResult(TransportInfrastructure);
            public void CustomizeAuditEndpoint(NServiceBus.EndpointConfiguration endpointConfiguration, TransportSettings transportSettings) => throw new NotImplementedException();
            public void CustomizeMonitoringEndpoint(NServiceBus.EndpointConfiguration endpointConfiguration, TransportSettings transportSettings) => throw new NotImplementedException();
            public void CustomizePrimaryEndpoint(NServiceBus.EndpointConfiguration endpointConfiguration, TransportSettings transportSettings) => throw new NotImplementedException();
            public Task ProvisionQueues(TransportSettings transportSettings, IEnumerable<string> additionalQueues) => throw new NotImplementedException();
            public string ToTransportQualifiedQueueName(string queueName) => queueName;
        }

        public class TestSender : IMessageDispatcher
        {
            public Action<UnicastTransportOperation> Callback { get; set; } = m => { };

            public Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction, CancellationToken cancellationToken)
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