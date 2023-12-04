namespace ServiceControl.Recoverability
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Connection;
    using Contracts.MessageFailures;
    using CustomChecks;
    using EventLog;
    using ExternalIntegration;
    using ExternalIntegrations;
    using Infrastructure.BackgroundTasks;
    using Infrastructure.DomainEvents;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using NServiceBus.Logging;
    using NServiceBus.Transport;
    using Operations;
    using Particular.ServiceControl;
    using Persistence;
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
                collection.AddSingleton<OperationsManager>();

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
                collection.AddHostedService(provider => provider.GetRequiredService<ReturnToSenderDequeuer>());

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
                collection.AddHostedService<FailedMessageNotificationsHostedService>();

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
            });
        }

        public override void Setup(Settings settings, IComponentInstallationContext context)
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
        }

        class FailedMessageNotificationsHostedService : IHostedService
        {
            public FailedMessageNotificationsHostedService(
                IDomainEvents domainEvents,
                IFailedMessageViewIndexNotifications store
                )
            {
                this.domainEvents = domainEvents;
                this.store = store;
            }

            public Task StartAsync(CancellationToken cancellationToken)
            {
                subscription = store.Subscribe(Callback);
                return Task.FromResult(true);
            }

            Task Callback(FailedMessageTotals message)
            {
                return domainEvents.Raise(new MessageFailuresUpdated
                {
                    UnresolvedTotal = message.UnresolvedTotal,
                    ArchivedTotal = message.UnresolvedTotal
                });
            }

            public Task StopAsync(CancellationToken cancellationToken)
            {
                subscription.Dispose();
                return Task.FromResult(true);
            }

            readonly IDomainEvents domainEvents;
            IFailedMessageViewIndexNotifications store;
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
                var processedRequests = await retries.ProcessNextBulkRetry();
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
            public RebuildRetryGroupStatusesHostedService(RetryDocumentManager retryDocumentManager)
            {
                this.retryDocumentManager = retryDocumentManager;
            }

            public Task StartAsync(CancellationToken cancellationToken)
            {
                return retryDocumentManager.RebuildRetryOperationState();
            }

            public Task StopAsync(CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            readonly RetryDocumentManager retryDocumentManager;
        }

        internal class AdoptOrphanBatchesFromPreviousSessionHostedService : IHostedService
        {
            public AdoptOrphanBatchesFromPreviousSessionHostedService(RetryDocumentManager retryDocumentManager, IAsyncTimer scheduler)
            {
                this.retryDocumentManager = retryDocumentManager;
                this.scheduler = scheduler;
            }

            internal async Task<bool> AdoptOrphanedBatchesAsync()
            {
                var moreWorkRemaining = await retryDocumentManager.AdoptOrphanedBatches();

                return moreWorkRemaining;
            }

            public Task StartAsync(CancellationToken cancellationToken)
            {
                timer = scheduler.Schedule(async _ =>
                {
                    var hasMoreWork = await AdoptOrphanedBatchesAsync();
                    return hasMoreWork ? TimerJobExecutionResult.ScheduleNextExecution : TimerJobExecutionResult.DoNotContinueExecuting;
                }, TimeSpan.Zero, TimeSpan.FromMinutes(2), e => { log.Error("Unhandled exception while trying to adopt orphaned batches", e); });
                return Task.CompletedTask;
            }

            public Task StopAsync(CancellationToken cancellationToken)
            {
                return timer.Stop();
            }

            TimerJob timer;
            readonly IAsyncTimer scheduler;
            readonly RetryDocumentManager retryDocumentManager;
            static readonly ILog log = LogManager.GetLogger<AdoptOrphanBatchesFromPreviousSessionHostedService>();
        }

        class ProcessRetryBatchesHostedService : IHostedService
        {
            public ProcessRetryBatchesHostedService(
                RetryProcessor processor,
                Settings settings,
                IAsyncTimer scheduler)
            {
                this.processor = processor;
                this.settings = settings;
                this.scheduler = scheduler;
            }

            public Task StartAsync(CancellationToken cancellationToken)
            {
                timer = scheduler.Schedule(Process, TimeSpan.Zero, settings.ProcessRetryBatchesFrequency, e => { log.Error("Unhandled exception while processing retry batches", e); });
                return Task.CompletedTask;
            }

            public async Task StopAsync(CancellationToken cancellationToken)
            {
                await timer.Stop();
            }

            async Task<TimerJobExecutionResult> Process(CancellationToken cancellationToken)
            {
                var batchesProcessed = await processor.ProcessBatches(cancellationToken);
                return batchesProcessed ? TimerJobExecutionResult.ExecuteImmediately : TimerJobExecutionResult.ScheduleNextExecution;
            }

            readonly Settings settings;
            readonly IAsyncTimer scheduler;
            TimerJob timer;

            RetryProcessor processor;
            static ILog log = LogManager.GetLogger(typeof(ProcessRetryBatchesHostedService));
        }
    }
}