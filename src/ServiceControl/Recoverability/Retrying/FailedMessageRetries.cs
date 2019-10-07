namespace ServiceControl.Recoverability
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure;
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.Logging;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Settings;

    public class FailedMessageRetries : Feature
    {
        public FailedMessageRetries()
        {
            Prerequisite(c =>
            {
                var settings = c.Settings.Get<Settings>("ServiceControl.Settings");
                return settings.RunRetryProcessor;
            }, "Failed message retry processing is disabled.");
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<RetryDocumentManager>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<RetriesGateway>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<RetryProcessor>(DependencyLifecycle.SingleInstance);

            context.RegisterStartupTask(b => b.Build<RebuildRetryGroupStatuses>());
            context.RegisterStartupTask(b => b.Build<BulkRetryBatchCreation>());
            context.RegisterStartupTask(b => b.Build<AdoptOrphanBatchesFromPreviousSession>());
            context.RegisterStartupTask(b => b.Build<ProcessRetryBatches>());
        }

        static ILog log = LogManager.GetLogger<FailedMessageRetries>();

        class BulkRetryBatchCreation : FeatureStartupTask
        {
            public BulkRetryBatchCreation(RetriesGateway retries)
            {
                this.retries = retries;
            }

            protected override Task OnStart(IMessageSession session)
            {
                if (retries != null)
                {
                    timer = new AsyncTimer(_ => ProcessRequestedBulkRetryOperations(), interval, interval, e => { log.Error("Unhandled exception while processing bulk retry operations", e); });
                }

                return Task.FromResult(0);
            }

            protected override Task OnStop(IMessageSession session)
            {
                return timer?.Stop() ?? Task.CompletedTask;
            }

            async Task<TimerJobExecutionResult> ProcessRequestedBulkRetryOperations()
            {
                var processedRequests = await retries.ProcessNextBulkRetry().ConfigureAwait(false);
                return processedRequests ? TimerJobExecutionResult.ExecuteImmediately : TimerJobExecutionResult.ScheduleNextExecution;
            }

            RetriesGateway retries;
            AsyncTimer timer;
            static TimeSpan interval = TimeSpan.FromSeconds(5);
        }

        class RebuildRetryGroupStatuses : FeatureStartupTask
        {
            public RebuildRetryGroupStatuses(RetryDocumentManager retryDocumentManager, IDocumentStore store)
            {
                this.store = store;
                this.retryDocumentManager = retryDocumentManager;
            }

            protected override async Task OnStart(IMessageSession session)
            {
                using (var storageSession = store.OpenAsyncSession())
                {
                    await retryDocumentManager.RebuildRetryOperationState(storageSession).ConfigureAwait(false);
                }
            }

            protected override Task OnStop(IMessageSession session)
            {
                return Task.FromResult(0);
            }

            RetryDocumentManager retryDocumentManager;
            IDocumentStore store;
        }

        internal class AdoptOrphanBatchesFromPreviousSession : FeatureStartupTask
        {
            public AdoptOrphanBatchesFromPreviousSession(RetryDocumentManager retryDocumentManager, IDocumentStore store)
            {
                this.retryDocumentManager = retryDocumentManager;
                this.store = store;
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

            protected override Task OnStart(IMessageSession session)
            {
                timer = new AsyncTimer(async _ =>
                {
                    var hasMoreWork = await AdoptOrphanedBatchesAsync().ConfigureAwait(false);
                    return hasMoreWork ? TimerJobExecutionResult.ScheduleNextExecution : TimerJobExecutionResult.DoNotContinueExecuting;
                }, TimeSpan.Zero, TimeSpan.FromMinutes(2), e => { log.Error("Unhandled exception while trying to adopt orphaned batches", e); });
                return Task.FromResult(0);
            }

            protected override Task OnStop(IMessageSession session)
            {
                return timer.Stop();
            }

            AsyncTimer timer;
            DateTime startTime;
            IDocumentStore store;
            RetryDocumentManager retryDocumentManager;
        }

        class ProcessRetryBatches : FeatureStartupTask
        {
            public ProcessRetryBatches(IDocumentStore store, RetryProcessor processor, Settings settings)
            {
                this.processor = processor;
                this.store = store;
                this.settings = settings;
            }

            protected override Task OnStart(IMessageSession session)
            {
                timer = new AsyncTimer(t => Process(t), TimeSpan.Zero, settings.ProcessRetryBatchesFrequency, e => { log.Error("Unhandled exception while processing retry batches", e); });
                return Task.FromResult(0);
            }

            protected override Task OnStop(IMessageSession session)
            {
                return timer.Stop();
            }

            async Task<TimerJobExecutionResult> Process(CancellationToken token)
            {
                using (var session = store.OpenAsyncSession())
                {
                    var batchesProcessed = await processor.ProcessBatches(session, token).ConfigureAwait(false);
                    await session.SaveChangesAsync().ConfigureAwait(false);
                    return batchesProcessed ? TimerJobExecutionResult.ExecuteImmediately : TimerJobExecutionResult.ScheduleNextExecution;
                }
            }

            readonly Settings settings;
            AsyncTimer timer;

            IDocumentStore store;
            RetryProcessor processor;
            static ILog log = LogManager.GetLogger(typeof(ProcessRetryBatches));
        }
    }
}