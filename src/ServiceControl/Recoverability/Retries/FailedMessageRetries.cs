namespace ServiceControl.Recoverability
{
    using System;
    using System.Threading;
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
            RegisterStartupTask<RebuildRetryGroupStatuses>();
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
                    processedRequests = retries.ProcessNextBulkRetry();
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
                using (var session = store.OpenSession())
                {
                    retryDocumentManager.RebuildRetryOperationState(session);
                }
            }

            RetryDocumentManager retryDocumentManager;
            IDocumentStore store;
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
                using (var session = store.OpenSession())
                {
                    retryDocumentManager.RebuildRetryGroupState(session);
                }
            }

            RetryDocumentManager retryDocumentManager;
            IDocumentStore store;
        }

        class AdoptOrphanBatchesFromPreviousSession : FeatureStartupTask
        {
            private Timer timer;

            public AdoptOrphanBatchesFromPreviousSession(RetryDocumentManager retryDocumentManager, TimeKeeper timeKeeper, IDocumentStore store)
            {
                this.retryDocumentManager = retryDocumentManager;
                this.timeKeeper = timeKeeper;
                this.store = store;
            }

            private bool AdoptOrphanedBatches()
            {
                bool hasMoreWorkToDo;

                using (var session = store.OpenSession())
                {
                    retryDocumentManager.AdoptOrphanedBatches(session, out hasMoreWorkToDo);
                }

                if (!hasMoreWorkToDo)
                {
                    //Disable timeout
                    timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                }

                return hasMoreWorkToDo;
            }

            protected override void OnStart()
            {
                timer = timeKeeper.NewTimer(AdoptOrphanedBatches, TimeSpan.Zero, TimeSpan.FromMinutes(2));
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
                abortProcessing = true;
                timeKeeper.Release(timer);
            }

            void Process()
            {
                try
                {
                    bool batchesProcessed;
                    do
                    {
                        using (var session = store.OpenSession())
                        {
                            batchesProcessed = processor.ProcessBatches(session);
                            session.SaveChanges();
                        }
                    } while (batchesProcessed && !abortProcessing);
                }
                catch (Exception ex)
                {
                    log.Error("Error during retry batch processing", ex);
                }
            }

            IDocumentStore store;
            RetryProcessor processor;
            private readonly TimeKeeper timeKeeper;
            private bool abortProcessing;
        }
    }
}
