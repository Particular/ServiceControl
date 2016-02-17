﻿namespace ServiceControl.Recoverability
{
    using System;
    using System.Threading;
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.Logging;
    using Raven.Client;
    using ServiceControl.Infrastructure;

    public class FailedMessageRetries : Feature
    {
        public FailedMessageRetries()
        {
            EnableByDefault();
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
                    processedRequests = retries.ProcessNextBulkRetry();
                } while (processedRequests && !abortProcessing);
            }
        }

        class AdoptOrphanBatchesFromPreviousSession : FeatureStartupTask
        {
            private Timer timer;

            public AdoptOrphanBatchesFromPreviousSession(RetryDocumentManager retryDocumentManager, TimeKeeper timeKeeper)
            {
                this.retryDocumentManager = retryDocumentManager;
                this.timeKeeper = timeKeeper;
            }

            private void AdoptOrphanedBatches()
            {
                var allDone = retryDocumentManager.AdoptOrphanedBatches();

                if (allDone)
                {
                    //Disable timeout
                    timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                }
            }

            protected override void OnStart()
            {
                timer = timeKeeper.New(AdoptOrphanedBatches, TimeSpan.Zero, TimeSpan.FromMinutes(2));
            }

            protected override void OnStop()
            {
                timeKeeper.Release(timer);
            }

            RetryDocumentManager retryDocumentManager;
            private readonly TimeKeeper timeKeeper;
        }

        class ProcessRetryBatches : FeatureStartupTask
        {
            static ILog log = LogManager.GetLogger(typeof(ProcessRetryBatches));
            private Timer timer;
            public ProcessRetryBatches(IDocumentStore store, RetryProcessor processor, TimeKeeper timeKeeper)
            {
                this.processor = processor;
                this.timeKeeper = timeKeeper;
                this.store = store;
            }

            protected override void OnStart()
            {
                timer = timeKeeper.New(Process, TimeSpan.Zero, TimeSpan.FromSeconds(30));
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
