namespace ServiceControl.Recoverability
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Connection;
    using Contracts;
    using CustomChecks;
    using EventLog;
    using ExternalIntegration;
    using ExternalIntegrations;
    using Infrastructure.BackgroundTasks;
    using Infrastructure.DomainEvents;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Operations;
    using Particular.ServiceControl;
    using Retrying;
    using ServiceBus.Management.Infrastructure.Settings;
    using Transports;
    using FailedMessagesUnArchived = Contracts.FailedMessagesUnArchived;
    using MessageEditedAndRetried = Contracts.MessageEditedAndRetried;
    using MessageFailed = Contracts.MessageFailed;
    using MessageFailureResolvedByRetry = Contracts.MessageFailureResolvedByRetry;
    using MessageFailureResolvedManually = Contracts.MessageFailureResolvedManually;

    class RecoverabilityComponent : ServiceControlComponent
    {
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

            // Integration Events
            if (!settings.DisableExternalIntegrationsPublishing)
            {
                context.AddEventPublished<FailedMessagesArchived>();
                context.AddEventPublished<FailedMessagesUnArchived>();
                context.AddEventPublished<MessageFailed>();
                context.AddEventPublished<MessageFailureResolvedByRetry>();
                context.AddEventPublished<MessageFailureResolvedManually>();
                context.AddEventPublished<MessageEditedAndRetried>();
            }
        }

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

            if (!settings.ErrorIngestionOnly)
            {
                services.AddHostedService(provider => provider.GetRequiredService<ReturnToSenderDequeuer>());
            }

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
            services.AddIntegrationEventPublisher<MessageEditedAndRetriedPublisher>();

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

        class BulkRetryBatchCreationHostedService : IHostedService
        {
            public BulkRetryBatchCreationHostedService(RetriesGateway retries, IAsyncTimer scheduler, ILogger<BulkRetryBatchCreationHostedService> logger)
            {
                this.retries = retries;
                this.scheduler = scheduler;
                this.logger = logger;
            }

            public Task StartAsync(CancellationToken cancellationToken = default)
            {
                if (retries != null)
                {
                    timer = scheduler.Schedule(ProcessRequestedBulkRetryOperations, interval, interval, e => logger.LogError(e, "Unhandled exception while processing bulk retry operations"));
                }

                return Task.CompletedTask;
            }

            public Task StopAsync(CancellationToken cancellationToken = default) => timer?.Stop(cancellationToken) ?? Task.CompletedTask;

            async Task<TimerJobExecutionResult> ProcessRequestedBulkRetryOperations(CancellationToken cancellationToken)
            {
                var processedRequests = await retries.ProcessNextBulkRetry(cancellationToken);
                return processedRequests ? TimerJobExecutionResult.ExecuteImmediately : TimerJobExecutionResult.ScheduleNextExecution;
            }

            RetriesGateway retries;
            IAsyncTimer scheduler;
            TimerJob timer;
            static TimeSpan interval = TimeSpan.FromSeconds(5);
            readonly ILogger<BulkRetryBatchCreationHostedService> logger;
        }

        class RebuildRetryGroupStatusesHostedService : IHostedService
        {
            public RebuildRetryGroupStatusesHostedService(RetryDocumentManager retryDocumentManager)
            {
                this.retryDocumentManager = retryDocumentManager;
            }

            public Task StartAsync(CancellationToken cancellationToken = default)
            {
                return retryDocumentManager.RebuildRetryOperationState(cancellationToken);
            }

            public Task StopAsync(CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            readonly RetryDocumentManager retryDocumentManager;
        }

        internal class AdoptOrphanBatchesFromPreviousSessionHostedService : IHostedService
        {
            public AdoptOrphanBatchesFromPreviousSessionHostedService(RetryDocumentManager retryDocumentManager, IAsyncTimer scheduler, ILogger<AdoptOrphanBatchesFromPreviousSessionHostedService> logger)
            {
                this.retryDocumentManager = retryDocumentManager;
                this.scheduler = scheduler;
                this.logger = logger;
            }

            internal async Task<bool> AdoptOrphanedBatchesAsync(CancellationToken cancellationToken = default)
            {
                var moreWorkRemaining = await retryDocumentManager.AdoptOrphanedBatches(cancellationToken);

                return moreWorkRemaining;
            }

            public Task StartAsync(CancellationToken cancellationToken = default)
            {
                timer = scheduler.Schedule(async token =>
                {
                    var hasMoreWork = await AdoptOrphanedBatchesAsync(token);
                    return hasMoreWork ? TimerJobExecutionResult.ScheduleNextExecution : TimerJobExecutionResult.DoNotContinueExecuting;
                }, TimeSpan.Zero, TimeSpan.FromMinutes(2), e => logger.LogError(e, "Unhandled exception while trying to adopt orphaned batches"));
                return Task.CompletedTask;
            }

            public Task StopAsync(CancellationToken cancellationToken = default) => timer.Stop(cancellationToken);

            TimerJob timer;
            readonly IAsyncTimer scheduler;
            readonly RetryDocumentManager retryDocumentManager;
            readonly ILogger<AdoptOrphanBatchesFromPreviousSessionHostedService> logger;
        }

        class ProcessRetryBatchesHostedService : IHostedService
        {
            public ProcessRetryBatchesHostedService(
                RetryProcessor processor,
                Settings settings,
                IAsyncTimer scheduler,
                ILogger<ProcessRetryBatchesHostedService> logger)
            {
                this.processor = processor;
                this.settings = settings;
                this.scheduler = scheduler;
                this.logger = logger;
            }

            public Task StartAsync(CancellationToken cancellationToken = default)
            {
                timer = scheduler.Schedule(Process, TimeSpan.Zero, settings.ProcessRetryBatchesFrequency, e => logger.LogError(e, "Unhandled exception while processing retry batches"));
                return Task.CompletedTask;
            }

            public Task StopAsync(CancellationToken cancellationToken = default) => timer.Stop(cancellationToken);

            async Task<TimerJobExecutionResult> Process(CancellationToken cancellationToken)
            {
                var batchesProcessed = await processor.ProcessBatches(cancellationToken);
                return batchesProcessed ? TimerJobExecutionResult.ExecuteImmediately : TimerJobExecutionResult.ScheduleNextExecution;
            }

            readonly Settings settings;
            readonly IAsyncTimer scheduler;
            TimerJob timer;

            RetryProcessor processor;
            readonly ILogger<ProcessRetryBatchesHostedService> logger;
        }
    }
}