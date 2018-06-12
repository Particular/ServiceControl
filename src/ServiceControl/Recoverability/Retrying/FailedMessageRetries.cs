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

            protected override Task OnStart(IMessageSession session)
            {
                if (retries != null)
                {
                    var due = TimeSpan.FromSeconds(5);
                    timer = timeKeeper.New(ProcessRequestedBulkRetryOperations, due, due);
                }

                return Task.FromResult(0);
            }

            protected override Task OnStop(IMessageSession session)
            {
                if (retries != null)
                {
                    abortProcessing = true;
                    timeKeeper.Release(timer);
                }
                
                return Task.FromResult(0);
            }

            async Task ProcessRequestedBulkRetryOperations()
            {
                bool processedRequests;
                do
                {
                    processedRequests = await retries.ProcessNextBulkRetry()
                        .ConfigureAwait(false);
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

            protected override Task OnStart(IMessageSession session)
            {
                timer = timeKeeper.NewTimer(() => AdoptOrphanedBatches(), TimeSpan.Zero, TimeSpan.FromMinutes(2));
                return Task.FromResult(0);
            }

            protected override Task OnStop(IMessageSession session)
            {
                timeKeeper.Release(timer);
                return Task.FromResult(0);
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

            protected override Task OnStart(IMessageSession session)
            {
                timer = timeKeeper.New(Process, TimeSpan.Zero, settings.ProcessRetryBatchesFrequency);
                return Task.FromResult(0);
            }

            protected override Task OnStop(IMessageSession session)
            {
                shuttingDown.Cancel();
                timeKeeper.Release(timer);
                return Task.FromResult(0);
            }

            async Task Process()
            {
                try
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
                catch (Exception ex)
                {
                    log.Error("Error during retry batch processing", ex);
                }
            }

            IDocumentStore store;
            RetryProcessor processor;
            private readonly TimeKeeper timeKeeper;
            private CancellationTokenSource shuttingDown = new CancellationTokenSource();
        }
    }
}
