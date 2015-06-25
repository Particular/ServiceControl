namespace ServiceControl.Recoverability.Retries
{
    using System;
    using System.Linq;
    using NServiceBus;
    using NServiceBus.Logging;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using ServiceControl.Shell.Api;

    public class RetryProcessor : IWantToRunWhenBusStartsAndStops, IDisposable
    {
        PeriodicExecutor executor;
        IDocumentStore store;

        public RetryProcessor(IDocumentStore store)
        {
            executor = new PeriodicExecutor(Process, TimeSpan.FromSeconds(30));
            this.store = store;
        }

        private void Process(PeriodicExecutor e)
        {
            bool batchesProcessed;
            do
            {
                using (var session = store.OpenSession())
                {
                    batchesProcessed = ProcessBatches(session);
                    session.SaveChanges();
                }
            } while (batchesProcessed && !e.IsCancellationRequested);
            UpdateOldBatches();
        }

        void UpdateOldBatches()
        {
            var cutOff = DateTimeOffset.UtcNow.AddMinutes(-5);

            var indexName = new RetryBatches_ByStatusAndDateStarted().IndexName;
            store.DatabaseCommands.UpdateByIndex(indexName,
                new IndexQuery
                {
                    Query = String.Format("Started:[NULL TO {0:s}]", cutOff)
                },
                new[]
                    {
                        new PatchRequest
                        {
                            Type = PatchCommandType.Set,
                            Name = "Status",
                            Value = (int) RetryBatchStatus.Staging, 
                            PrevVal = (int)RetryBatchStatus.MarkingDocuments
                        },
                    },
                true);
        }

        bool ProcessBatches(IDocumentSession session)
        {
            // TODO: Make this query only what we need
            var batches = session.Query<RetryBatch>()
                .ToArray()
                .ToLookup(x => x.Status);

            var forwardingBatch = batches[RetryBatchStatus.Forwarding].SingleOrDefault();
            if (forwardingBatch != null)
            {
                Forward(forwardingBatch);
                return true;
            }

            var stagingBatch = batches[RetryBatchStatus.Staging].FirstOrDefault();
            if (stagingBatch != null)
            {
                Stage(stagingBatch);
                return true;
            }

            return false;
        }

        void Stage(RetryBatch batch)
        {
            // TODO: Clear Staging Queue and Fill it up using the streaming API
            batch.Status = RetryBatchStatus.Forwarding;
            // TODO: Issue a message indicating the status change?
            Log.InfoFormat("Retry batch {0} staged", batch.Id);
        }

        void Forward(RetryBatch batch)
        {
           // TODO: Process the staging queue
            batch.Status = RetryBatchStatus.Done;
            // TODO: Issue a message indicating completion?
            // TODO: Delete the Retry Batch when done?
            Log.InfoFormat("Retry batch {0} done", batch.Id);
        }


        public void Start()
        {
            executor.Start(delay: false);
        }

        public void Stop()
        {
            executor.Stop();
        }

        public void Dispose()
        {
            Stop();
        }

        static ILog Log = LogManager.GetLogger(typeof(RetryProcessor));
    }
}
