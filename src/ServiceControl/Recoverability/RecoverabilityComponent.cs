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
    using Operations;
    using Particular.ServiceControl;
    using Persistence;
    using Retrying;
    using ServiceBus.Management.Infrastructure.Settings;
    using Transports;

    class RecoverabilityComponent : ServiceControlComponent
    {
        public override void Configure(Settings settings, ITransportCustomization transportCustomization, IHostApplicationBuilder hostBuilder)
        {
            var services = hostBuilder.Services;
            services.AddPlatformConnectionProvider<RecoverabilityPlatformConnectionDetailsProvider>();

            //Archiving
            services.AddSingleton<OperationsManager>();

            //Grouping
            services.AddSingleton<IFailureClassifier, ExceptionTypeAndStackTraceFailureClassifier>();
            services.AddSingleton<IFailureClassifier, MessageTypeFailureClassifier>();
            services.AddSingleton<IFailureClassifier, AddressOfFailingEndpointClassifier>();
            services.AddSingleton<IFailureClassifier, EndpointInstanceClassifier>();
            services.AddSingleton<IFailureClassifier, EndpointNameClassifier>();
            services.AddSingleton<IFailedMessageEnricher, ClassifyFailedMessageEnricher>();

            //Retrying
            services.AddSingleton<RetryingManager>();
            services.AddSingleton<GroupFetcher>();
            services.AddDomainEventHandler<StoreHistoryHandler>();
            services.AddDomainEventHandler<FailedMessageRetryCleaner>();

            //Return to sender - registered both as singleton and hosted service because it is a dependency of the RetryProcessor
            services.AddSingleton<ReturnToSender>();
            services.AddSingleton<ErrorQueueNameCache>();
            services.AddSingleton<ReturnToSenderDequeuer>();
            services.AddHostedService(provider => provider.GetRequiredService<ReturnToSenderDequeuer>());

            //Error importer
            services.AddSingleton<ImportFailedErrors>();
            services.AddSingleton<ErrorIngestor>();
            services.AddSingleton<ErrorIngestionCustomCheck.State>();
            if (settings.IngestErrorMessages)
            {
                services.AddHostedService<ErrorIngestion>();
            }

            //Retries
            services.AddSingleton<RetryDocumentManager>();
            services.AddSingleton<RetriesGateway>();
            services.AddSingleton<RetryProcessor>();
            if (settings.RunRetryProcessor)
            {
                services.AddHostedService<RebuildRetryGroupStatusesHostedService>();
                services.AddHostedService<BulkRetryBatchCreationHostedService>();
                services.AddHostedService<AdoptOrphanBatchesFromPreviousSessionHostedService>();
                services.AddHostedService<ProcessRetryBatchesHostedService>();
            }

            //Failed messages
            services.AddHostedService<FailedMessageNotificationsHostedService>();

            //Health checks
            services.AddCustomCheck<ErrorIngestionCustomCheck>();
            services.AddCustomCheck<FailedErrorImportCustomCheck>();

            //External integration
            services.AddIntegrationEventPublisher<FailedMessageArchivedPublisher>();
            services.AddIntegrationEventPublisher<FailedMessageGroupBatchArchivedPublisher>();
            services.AddIntegrationEventPublisher<FailedMessageGroupBatchUnarchivedPublisher>();
            services.AddIntegrationEventPublisher<FailedMessagesUnarchivedPublisher>();
            services.AddIntegrationEventPublisher<MessageFailedPublisher>();
            services.AddIntegrationEventPublisher<MessageFailureResolvedByRetryPublisher>();
            services.AddIntegrationEventPublisher<MessageFailureResolvedManuallyPublisher>();

            //Event log
            services.AddEventLogMapping<FailedMessageArchivedDefinition>();
            services.AddEventLogMapping<FailedMessageGroupArchivedDefinition>();
            services.AddEventLogMapping<FailedMessageGroupUnarchivedDefinition>();
            services.AddEventLogMapping<FailedMessageUnArchivedDefinition>();
            services.AddEventLogMapping<MessageFailedDefinition>();
            services.AddEventLogMapping<MessageFailedInStagingDefinition>();
            services.AddEventLogMapping<MessageFailureResolvedByRetryDefinition>();
            services.AddEventLogMapping<MessageFailureResolvedManuallyDefinition>();
            services.AddEventLogMapping<MessageRedirectChangedDefinition>();
            services.AddEventLogMapping<MessageRedirectCreatedDefinition>();
            services.AddEventLogMapping<MessageRedirectRemovedDefinition>();
            services.AddEventLogMapping<MessageSubmittedForRetryDefinition>();
            services.AddEventLogMapping<MessagesSubmittedForRetryDefinition>();
            services.AddEventLogMapping<MessagesSubmittedForRetryFailedDefinition>();
        }

        public override void Setup(Settings settings, IComponentInstallationContext context, IHostApplicationBuilder hostBuilder)
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

            public Task StopAsync(CancellationToken cancellationToken) => timer?.Stop(cancellationToken) ?? Task.CompletedTask;

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

            public Task StopAsync(CancellationToken cancellationToken) => timer.Stop(cancellationToken);

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

            public Task StopAsync(CancellationToken cancellationToken) => timer.Stop(cancellationToken);

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