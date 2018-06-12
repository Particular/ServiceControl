namespace ServiceControl.Recoverability
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.Logging;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Infrastructure;

    public class FailedMessageRetries : Feature
    {
        public FailedMessageRetries()
        {
            EnableByDefault();
            RegisterStartupTask<RebuildRetryGroupStatuses>();
            RegisterStartupTask<BulkRetryBatchCreation>();
            RegisterStartupTask<AdoptOrphanBatchesFromPreviousSession>();
            RegisterStartupTask<ProcessRetryBatches>();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<RetryDocumentManager>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<RetriesGateway>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<RetryProcessor>(DependencyLifecycle.SingleInstance);
        }

        class BulkRetryBatchCreation : FeatureStartupTask
        {
            readonly RetriesGateway retries;
            private readonly TimeKeeper timeKeeper;
            private Timer timer;
            private bool abortProcessing;

            public BulkRetryBatchCreation(RetriesGateway retries, TimeKeeper timeKeeper)
            {
                this.retries = retries;
                this.timeKeeper = timeKeeper;
            }

            protected override void OnStart()
            {
                if (retries != null)
                {
                    var due = TimeSpan.FromSeconds(5);
                    timer = timeKeeper.New(ProcessRequestedBulkRetryOperations, due, due);
                }
            }

            protected override void OnStop()
            {
                if (retries != null)
                {
                    abortProcessing = true;
                    timeKeeper.Release(timer);
                }
            }

            void ProcessRequestedBulkRetryOperations()
            {
                bool processedRequests;
                do
                {
                    processedRequests = retries.ProcessNextBulkRetry().GetAwaiter().GetResult();
                } while (processedRequests && !abortProcessing);
            }
        }

        class RebuildRetryGroupStatuses : FeatureStartupTask
        {
            public RebuildRetryGroupStatuses(RetryDocumentManager retryDocumentManager, IDocumentStore store)
            {
                this.store = store;
                this.retryDocumentManager = retryDocumentManager;
            }

            protected override void OnStart()
            {
                StartAsync().GetAwaiter().GetResult();
            }

            private async Task StartAsync()
            {
                using (var session = store.OpenAsyncSession())
                {
                    await retryDocumentManager.RebuildRetryOperationState(session).ConfigureAwait(false);
                }
            }

            RetryDocumentManager retryDocumentManager;
            IDocumentStore store;
        }

        internal class AdoptOrphanBatchesFromPreviousSession : FeatureStartupTask
        {
            private Timer timer;
            private DateTime startTime;

            public AdoptOrphanBatchesFromPreviousSession(RetryDocumentManager retryDocumentManager, TimeKeeper timeKeeper, IDocumentStore store)
            {
                this.retryDocumentManager = retryDocumentManager;
                this.timeKeeper = timeKeeper;
                this.store = store;
                startTime = DateTime.UtcNow;
            }

            async Task<bool> AdoptOrphanedBatches()
            {
                var hasMoreWork = await AdoptOrphanedBatchesAsync().ConfigureAwait(false);

                if (!hasMoreWork)
                {
                    //Disable timeout
                    timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                }

                return hasMoreWork;
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

            protected override void OnStart()
            {
                timer = timeKeeper.NewTimer(() => AdoptOrphanedBatches(), TimeSpan.Zero, TimeSpan.FromMinutes(2));
            }

            protected override void OnStop()
            {
                timeKeeper.Release(timer);
            }

            IDocumentStore store;
            RetryDocumentManager retryDocumentManager;
            private readonly TimeKeeper timeKeeper;
        }

        class ProcessRetryBatches : FeatureStartupTask
        {
            static ILog log = LogManager.GetLogger(typeof(ProcessRetryBatches));
            private Timer timer;
            private readonly Settings settings;

            public ProcessRetryBatches(IDocumentStore store, RetryProcessor processor, TimeKeeper timeKeeper, Settings settings)
            {
                this.processor = processor;
                this.timeKeeper = timeKeeper;
                this.store = store;
                this.settings = settings;
            }

            protected override void OnStart()
            {
                timer = timeKeeper.New(Process, TimeSpan.Zero, settings.ProcessRetryBatchesFrequency);
            }

            protected override void OnStop()
            {
                shuttingDown.Cancel();
                timeKeeper.Release(timer);
            }

            void Process()
            {
                try
                {
                    ProcessAsync().GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    log.Error("Error during retry batch processing", ex);
                }
            }

            private async Task ProcessAsync()
            {
                bool batchesProcessed;
                do
                {
                    using (var session = store.OpenAsyncSession())
                    {
                        batchesProcessed = await processor.ProcessBatches(session, shuttingDown.Token).ConfigureAwait(false);
                        await session.SaveChangesAsync().ConfigureAwait(false);
                    }
                } while (batchesProcessed && !shuttingDown.IsCancellationRequested);
            }

            IDocumentStore store;
            RetryProcessor processor;
            private readonly TimeKeeper timeKeeper;
            private CancellationTokenSource shuttingDown = new CancellationTokenSource();
        }
    }
}
