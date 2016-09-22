namespace ServiceControl.Operations.Audit
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Metrics;
    using NServiceBus;
    using NServiceBus.Logging;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using Raven.Client.Document;
    using ServiceControl.Operations.BodyStorage;

    class ProcessAudits
    {
        private const int BATCH_SIZE = 128;

        private ILog logger = LogManager.GetLogger<ProcessAudits>();

        private readonly IDocumentStore store;
        private Task task;
        private volatile bool stop;
        private readonly Meter meter = Metric.Meter("Audit messages processed", Unit.Custom("Messages"));

        AuditIngestionCache auditIngestionCache;
        ProcessedMessageFactory processedMessageFactory;
        private readonly CriticalError criticalError;
        private RepeatedFailuresOverTimeCircuitBreaker breaker;

        public ProcessAudits(IDocumentStore store, AuditIngestionCache auditIngestionCache, ProcessedMessageFactory processedMessageFactory, CriticalError criticalError)
        {
            this.store = store;
            this.auditIngestionCache = auditIngestionCache;

            this.processedMessageFactory = processedMessageFactory;
            this.criticalError = criticalError;
        }

        public void Start()
        {
            breaker = new RepeatedFailuresOverTimeCircuitBreaker("ProcessAudits", TimeSpan.FromMinutes(2), ex =>
                {
                    stop = true;
                    criticalError.Raise("Repeated failures when processing audits.", ex);
                },
                TimeSpan.FromSeconds(2));
            stop = false;
            task = ProcessWithRetries();
        }

        public void Stop()
        {
            stop = true;
            task.Wait();
            breaker.Dispose();
        }

        private async Task ProcessWithRetries()
        {
            do
            {
                try
                {
                    await Process().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger.Warn("ProcessAudits failed, having a break for 2 seconds before trying again.", ex);
                    await breaker.Failure(ex).ConfigureAwait(false);
                    logger.Warn("Restarting ProcessAudits.");
                }
            } while (!stop);
        }

        private async Task Process()
        {
            var processedFiles = new List<string>(BATCH_SIZE);

            do
            {
                processedFiles.Clear();
                var bulkInsertLazy = new Lazy<BulkInsertOperation>(() => store.BulkInsert(options: new BulkInsertOptions
                {
                    WriteTimeoutMilliseconds = 2000
                }));

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
                }

                meter.Mark(processedFiles.Count);

                breaker.Success();

                if (!stop && processedFiles.Count < BATCH_SIZE)
                {
                    await Task.Delay(1000).ConfigureAwait(false);
                }
            } while (!stop);
        }
    }
}