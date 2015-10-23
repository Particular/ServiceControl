namespace ServiceControl.Recoverability
{
    using System;
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
            RegisterStartupTask<StopProcessingOutstandingBatchesAtShutdown>();
            RegisterStartupTask<AdoptOrphanBatchesFromPreviousSession>();
            RegisterStartupTask<ProcessRetryBatches>();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<RetryDocumentManager>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<RetriesGateway>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<RetryProcessor>(DependencyLifecycle.SingleInstance);
        }

        class StopProcessingOutstandingBatchesAtShutdown : FeatureStartupTask
        {
            readonly RetriesGateway retries;
            PeriodicExecutor retriesGatewayExecutor;

            public StopProcessingOutstandingBatchesAtShutdown(RetriesGateway retries)
            {
                this.retries = retries;

                retriesGatewayExecutor = new PeriodicExecutor(
                ProcessRequestedBulkRetryOperations,
                TimeSpan.FromSeconds(5));
            }

            protected override void OnStart()
            {
                if (retries != null)
                {
                    retriesGatewayExecutor.Start(true);
                }
            }

            protected override void OnStop()
            {
                if (retries != null)
                {
                    retriesGatewayExecutor.Stop();
                }
            }

            void ProcessRequestedBulkRetryOperations(PeriodicExecutor obj)
            {
                bool processedRequests;
                do
                {
                    processedRequests = retries.ProcessNextBulkRetry();
                } while (processedRequests && !obj.IsCancellationRequested);
            }
        }

        class AdoptOrphanBatchesFromPreviousSession : FeatureStartupTask
        {
            public AdoptOrphanBatchesFromPreviousSession(RetryDocumentManager retryDocumentManager)
            {
                this.retryDocumentManager = retryDocumentManager;
                executor = new PeriodicExecutor(
                    AdoptOrphanedBatches,
                    TimeSpan.FromMinutes(2)
                );
            }

            private void AdoptOrphanedBatches(PeriodicExecutor ex)
            {
                var allDone = retryDocumentManager.AdoptOrphanedBatches();

                if (allDone)
                {
                    executor.Stop();
                }
            }

            protected override void OnStart()
            {
                executor.Start(false);
            }

            protected override void OnStop()
            {
                executor.Stop();
            }

            PeriodicExecutor executor;
            RetryDocumentManager retryDocumentManager;
        }

        class ProcessRetryBatches : FeatureStartupTask
        {
            static ILog log = LogManager.GetLogger(typeof(ProcessRetryBatches));

            public ProcessRetryBatches(IDocumentStore store, RetryProcessor processor)
            {
                executor = new PeriodicExecutor(Process, TimeSpan.FromSeconds(30), ex => log.Error("Error during retry batch processing", ex));
                this.processor = processor;
                this.store = store;
            }

            protected override void OnStart()
            {
                executor.Start(false);
            }

            protected override void OnStop()
            {
                executor.Stop();
            }

            void Process(PeriodicExecutor e)
            {
                bool batchesProcessed;
                do
                {
                    using (var session = store.OpenSession())
                    {
                        batchesProcessed = processor.ProcessBatches(session);
                        session.SaveChanges();
                    }
                } while (batchesProcessed && !e.IsCancellationRequested);
            }

            PeriodicExecutor executor;
            IDocumentStore store;
            RetryProcessor processor;
        }
    }
}
