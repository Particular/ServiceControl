namespace ServiceControl.Recoverability
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Connection;
    using CustomChecks;
    using EventLog;
    using ExternalIntegration;
    using ExternalIntegrations;
    using Infrastructure.BackgroundTasks;
    using Infrastructure.DomainEvents;
    using Infrastructure.RavenDB;
    using MessageFailures;
    using MessageFailures.Api;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using NServiceBus.Logging;
    using NServiceBus.Raw;
    using Operations;
    using Operations.BodyStorage;
    using Operations.BodyStorage.RavenAttachments;
    using Particular.ServiceControl;
    using Raven.Client;
    using Retrying;
    using ServiceBus.Management.Infrastructure.Settings;

    class RecoverabilityComponent : ServiceControlComponent
    {
        public override void Configure(Settings settings, IHostBuilder hostBuilder)
        {
            hostBuilder.ConfigureServices(collection =>
            {
                collection.AddPlatformConnectionProvider<RecoverabilityPlatformConnectionDetailsProvider>();

                //Archiving
                collection.AddSingleton<ArchivingManager>();
                collection.AddSingleton<ArchiveDocumentManager>();

                collection.AddSingleton<OperationsManager>();
                collection.AddSingleton<UnarchivingManager>();
                collection.AddSingleton<UnarchiveDocumentManager>();

                //Grouping
                collection.AddSingleton<IFailureClassifier, ExceptionTypeAndStackTraceFailureClassifier>();
                collection.AddSingleton<IFailureClassifier, MessageTypeFailureClassifier>();
                collection.AddSingleton<IFailureClassifier, AddressOfFailingEndpointClassifier>();
                collection.AddSingleton<IFailureClassifier, EndpointInstanceClassifier>();
                collection.AddSingleton<IFailureClassifier, EndpointNameClassifier>();
                collection.AddSingleton<IFailedMessageEnricher, ClassifyFailedMessageEnricher>();

                //Retrying
                collection.AddSingleton<RetryingManager>();
                collection.AddSingleton<GroupFetcher>();
                collection.AddDomainEventHandler<StoreHistoryHandler>();
                collection.AddDomainEventHandler<FailedMessageRetryCleaner>();

                //Return to sender - registered both as singleton and hosted service because it is a dependency of the RetryProcessor
                collection.AddSingleton<ReturnToSenderDequeuer>();
                collection.AddHostedService<ReturnToSenderDequeuer>();

                //Error importer
                collection.AddSingleton<ErrorIngestor>();
                collection.AddSingleton<ErrorIngestionCustomCheck.State>();
                if (settings.IngestErrorMessages)
                {
                    collection.AddHostedService<ErrorIngestion>();
                }

                //Retries
                if (settings.RunRetryProcessor)
                {
                    collection.AddSingleton<RetryDocumentManager>();
                    collection.AddSingleton<RetriesGateway>();
                    collection.AddSingleton<RetryProcessor>();

                    collection.AddHostedService<RebuildRetryGroupStatusesHostedService>();
                    collection.AddHostedService<BulkRetryBatchCreationHostedService>();
                    collection.AddHostedService<AdoptOrphanBatchesFromPreviousSessionHostedService>();
                    collection.AddHostedService<ProcessRetryBatchesHostedService>();
                }

                //Failed messages
                collection.AddSingleton<FailedMessageViewIndexNotifications>();
                collection.AddHostedService<FailedMessageNotificationsHostedService>();

                //Body storage
                collection.AddSingleton<IBodyStorage, RavenAttachmentsBodyStorage>();
                collection.AddSingleton<BodyStorageEnricher>();

                //Health checks
                collection.AddCustomCheck<ErrorIngestionCustomCheck>();
                collection.AddCustomCheck<FailedErrorImportCustomCheck>();

                //External integration
                collection.AddIntegrationEventPublisher<FailedMessageArchivedPublisher>();
                collection.AddIntegrationEventPublisher<FailedMessageGroupBatchArchivedPublisher>();
                collection.AddIntegrationEventPublisher<FailedMessageGroupBatchUnarchivedPublisher>();
                collection.AddIntegrationEventPublisher<FailedMessagesUnarchivedPublisher>();
                collection.AddIntegrationEventPublisher<MessageFailedPublisher>();
                collection.AddIntegrationEventPublisher<MessageFailureResolvedByRetryPublisher>();
                collection.AddIntegrationEventPublisher<MessageFailureResolvedManuallyPublisher>();

                //Event log
                collection.AddEventLogMapping<FailedMessageArchivedDefinition>();
                collection.AddEventLogMapping<FailedMessageGroupArchivedDefinition>();
                collection.AddEventLogMapping<FailedMessageGroupUnarchivedDefinition>();
                collection.AddEventLogMapping<FailedMessageUnArchivedDefinition>();
                collection.AddEventLogMapping<MessageFailedDefinition>();
                collection.AddEventLogMapping<MessageFailedInStagingDefinition>();
                collection.AddEventLogMapping<MessageFailureResolvedByRetryDefinition>();
                collection.AddEventLogMapping<MessageFailureResolvedManuallyDefinition>();
                collection.AddEventLogMapping<MessageRedirectChangedDefinition>();
                collection.AddEventLogMapping<MessageRedirectCreatedDefinition>();
                collection.AddEventLogMapping<MessageRedirectRemovedDefinition>();
                collection.AddEventLogMapping<MessageSubmittedForRetryDefinition>();
                collection.AddEventLogMapping<MessagesSubmittedForRetryDefinition>();
                collection.AddEventLogMapping<MessagesSubmittedForRetryFailedDefinition>();
                collection.AddEventLogMapping<ReclassificationOfErrorMessageCompleteDefinition>();
            });
        }

        public override void Setup(Settings settings, IComponentSetupContext context)
        {
            context.CreateQueue(settings.StagingQueue);

            if (settings.IngestErrorMessages)
            {
                context.CreateQueue(settings.ErrorQueue);
            }

            if (settings.ForwardErrorMessages && settings.ErrorLogQueue != null)
            {
                context.CreateQueue(settings.ErrorLogQueue);
            }

            context.AddIndexAssembly(typeof(RavenBootstrapper).Assembly);
        }

        class FailedMessageNotificationsHostedService : IHostedService
        {
            public FailedMessageNotificationsHostedService(FailedMessageViewIndexNotifications notifications, IDocumentStore store)
            {
                this.notifications = notifications;
                this.store = store;
            }

            public Task StartAsync(CancellationToken cancellationToken)
            {
                subscription = store.Changes().ForIndex(new FailedMessageViewIndex().IndexName).Subscribe(notifications);
                return Task.FromResult(true);
            }

            public Task StopAsync(CancellationToken cancellationToken)
            {
                subscription.Dispose();
                return Task.FromResult(true);
            }

            FailedMessageViewIndexNotifications notifications;
            IDocumentStore store;
            IDisposable subscription;
        }

        class BulkRetryBatchCreationHostedService : IHostedService
        {
            public BulkRetryBatchCreationHostedService(RetriesGateway retries, IAsyncTimer scheduler)
            {
                this.retries = retries;
                this.scheduler = scheduler;
            }

            public Task StartAsync(CancellationToken cancellationToken)
            {
                if (retries != null)
                {
                    timer = scheduler.Schedule(_ => ProcessRequestedBulkRetryOperations(), interval, interval, e => { log.Error("Unhandled exception while processing bulk retry operations", e); });
                }

                return Task.CompletedTask;
            }

            public Task StopAsync(CancellationToken cancellationToken)
            {
                return timer?.Stop() ?? Task.CompletedTask;
            }

            async Task<TimerJobExecutionResult> ProcessRequestedBulkRetryOperations()
            {
                var processedRequests = await retries.ProcessNextBulkRetry().ConfigureAwait(false);
                return processedRequests ? TimerJobExecutionResult.ExecuteImmediately : TimerJobExecutionResult.ScheduleNextExecution;
            }

            RetriesGateway retries;
            IAsyncTimer scheduler;
            TimerJob timer;
            static TimeSpan interval = TimeSpan.FromSeconds(5);
            static ILog log = LogManager.GetLogger<BulkRetryBatchCreationHostedService>();
        }

        class RebuildRetryGroupStatusesHostedService : IHostedService
        {
            public RebuildRetryGroupStatusesHostedService(RetryDocumentManager retryDocumentManager, IDocumentStore store)
            {
                this.store = store;
                this.retryDocumentManager = retryDocumentManager;
            }

            public async Task StartAsync(CancellationToken cancellationToken)
            {
                using (var storageSession = store.OpenAsyncSession())
                {
                    await retryDocumentManager.RebuildRetryOperationState(storageSession).ConfigureAwait(false);
                }
            }

            public Task StopAsync(CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            RetryDocumentManager retryDocumentManager;
            IDocumentStore store;
        }

        internal class AdoptOrphanBatchesFromPreviousSessionHostedService : IHostedService
        {
            public AdoptOrphanBatchesFromPreviousSessionHostedService(RetryDocumentManager retryDocumentManager, IDocumentStore store, IAsyncTimer scheduler)
            {
                this.retryDocumentManager = retryDocumentManager;
                this.store = store;
                this.scheduler = scheduler;
                startTime = DateTime.UtcNow;
            }

            internal async Task<bool> AdoptOrphanedBatchesAsync()
            {
                bool hasMoreWorkToDo;
                using (var session = store.OpenAsyncSession())
                {
                    hasMoreWorkToDo = await retryDocumentManager.AdoptOrphanedBatches(session, startTime).ConfigureAwait(false);
                }

                return hasMoreWorkToDo;
            }
            public Task StartAsync(CancellationToken cancellationToken)
            {
                timer = scheduler.Schedule(async _ =>
                {
                    var hasMoreWork = await AdoptOrphanedBatchesAsync().ConfigureAwait(false);
                    return hasMoreWork ? TimerJobExecutionResult.ScheduleNextExecution : TimerJobExecutionResult.DoNotContinueExecuting;
                }, TimeSpan.Zero, TimeSpan.FromMinutes(2), e => { log.Error("Unhandled exception while trying to adopt orphaned batches", e); });
                return Task.CompletedTask;
            }

            public Task StopAsync(CancellationToken cancellationToken)
            {
                return timer.Stop();
            }

            TimerJob timer;
            DateTime startTime;
            IDocumentStore store;
            readonly IAsyncTimer scheduler;
            RetryDocumentManager retryDocumentManager;
            static ILog log = LogManager.GetLogger<AdoptOrphanBatchesFromPreviousSessionHostedService>();
        }

        class ProcessRetryBatchesHostedService : IHostedService
        {
            public ProcessRetryBatchesHostedService(IDocumentStore store, RetryProcessor processor, Settings settings, IAsyncTimer scheduler, RawEndpointFactory rawEndpointFactory)
            {
                this.processor = processor;
                this.store = store;
                this.settings = settings;
                this.scheduler = scheduler;
                this.rawEndpointFactory = rawEndpointFactory;
            }

            public async Task StartAsync(CancellationToken cancellationToken)
            {
                var senderConfig = rawEndpointFactory.CreateSendOnly("RetryProcessor");
                sender = await RawEndpoint.Start(senderConfig).ConfigureAwait(false);

                timer = scheduler.Schedule(t => Process(t), TimeSpan.Zero, settings.ProcessRetryBatchesFrequency, e => { log.Error("Unhandled exception while processing retry batches", e); });
            }

            public async Task StopAsync(CancellationToken cancellationToken)
            {
                await timer.Stop().ConfigureAwait(false);
                await sender.Stop().ConfigureAwait(false);
            }

            async Task<TimerJobExecutionResult> Process(CancellationToken cancellationToken)
            {
                using (var session = store.OpenAsyncSession())
                {
                    var batchesProcessed = await processor.ProcessBatches(session, sender, cancellationToken).ConfigureAwait(false);
                    await session.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
                    return batchesProcessed ? TimerJobExecutionResult.ExecuteImmediately : TimerJobExecutionResult.ScheduleNextExecution;
                }
            }

            readonly Settings settings;
            readonly IAsyncTimer scheduler;
            readonly RawEndpointFactory rawEndpointFactory;
            TimerJob timer;

            IDocumentStore store;
            RetryProcessor processor;
            static ILog log = LogManager.GetLogger(typeof(ProcessRetryBatchesHostedService));
            IReceivingRawEndpoint sender;
        }
    }
}