namespace ServiceControl.Operations
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Metrics;
    using Raven.Client;
    using Raven.Client.Document;
    using ServiceControl.Operations.Audit;
    using ServiceControl.Operations.BodyStorage;

    class ProcessAudits
    {
        private const int BATCH_SIZE = 128;

        private readonly IDocumentStore store;
        private Task task;
        private volatile bool stop;
        private readonly Meter meter = Metric.Meter("Audit messages processed", Unit.Results);

        AuditIngestionCache auditIngestionCache;
        ProcessedMessageFactory processedMessageFactory;

        public ProcessAudits(IDocumentStore store, AuditIngestionCache auditIngestionCache, ProcessedMessageFactory processedMessageFactory)
        {
            this.store = store;
            this.auditIngestionCache = auditIngestionCache;

            this.processedMessageFactory = processedMessageFactory;
        }

        public void Start()
        {
            stop = false;
            task = Process();
        }

        private async Task Process()
        {
            do
            {
                var processedFiles = new List<string>(BATCH_SIZE);

                var bulkInsertLazy = new Lazy<BulkInsertOperation>(() => store.BulkInsert());

                foreach (var entry in auditIngestionCache.GetBatch(BATCH_SIZE))
                {
                    if (stop)
                    {
                        break;
                    }

                    Dictionary<string, string> headers;
                    ClaimsCheck bodyStorageClaimsCheck;

                    if (auditIngestionCache.TryGet(entry, out headers, out bodyStorageClaimsCheck))
                    {
                        var processedMessage = processedMessageFactory.Create(headers);
                        processedMessageFactory.AddBodyDetails(processedMessage, bodyStorageClaimsCheck);

                        bulkInsertLazy.Value.Store(processedMessage);

                        processedFiles.Add(entry);
                    }
                }

                if (processedFiles.Count > 0)
                {
                    await bulkInsertLazy.Value.DisposeAsync().ConfigureAwait(false);
                    foreach (var file in processedFiles)
                    {
                        File.Delete(file);
                    }
                    meter.Mark(processedFiles.Count);
                }

                if (processedFiles.Count < BATCH_SIZE)
                {
                    await Task.Delay(1000).ConfigureAwait(false);
                }
            } while (!stop);
        }

        public void Stop()
        {
            stop = true;
            task.Wait();
        }
    }

}