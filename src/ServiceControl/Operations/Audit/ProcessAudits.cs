namespace ServiceControl.Operations.Audit
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Metrics;
    using NServiceBus;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using Raven.Client.Document;
    using ServiceControl.Operations.BodyStorage;

    class ProcessAudits
    {
        private const int BATCH_SIZE = 1024;

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
            breaker = new RepeatedFailuresOverTimeCircuitBreaker("ProcessAudits", TimeSpan.FromMinutes(5), ex =>
                {
                    stop = true;
                    criticalError.Raise("Repeated failures when processing audits.", ex);
                },
                TimeSpan.FromSeconds(40));
            stop = false;
            task = Process();
        }

        public void Stop()
        {
            stop = true;
            task.Wait();
            breaker.Dispose();
        }

        private async Task Process()
        {
            var processedFiles = new List<string>(BATCH_SIZE);
            do
            {
                try
                {
                    var lazyBulkInsert = new Lazy<BulkInsertOperation>(() => store.BulkInsert(options: new BulkInsertOptions
                    {
                        ChunkedBulkInsertOptions = null,
                        BatchSize = 128
                    }));

                    try
                    {
                        foreach (var file in auditIngestionCache.GetBatch(BATCH_SIZE))
                        {
                            Dictionary<string, string> headers;
                            ClaimsCheck bodyStorageClaimsCheck;

                            if (auditIngestionCache.TryGet(file, out headers, out bodyStorageClaimsCheck))
                            {
                                var processedMessage = processedMessageFactory.Create(headers);

                                processedMessageFactory.AddBodyDetails(processedMessage, bodyStorageClaimsCheck);

                                lazyBulkInsert.Value.Store(processedMessage);
                                processedFiles.Add(file);
                                meter.Mark();
                            }
                        }
                    }
                    finally
                    {
                        if (lazyBulkInsert.IsValueCreated)
                        {
                            await lazyBulkInsert.Value.DisposeAsync().ConfigureAwait(false);
                        }
                    }

                    foreach (var file in processedFiles)
                    {
                        File.Delete(file);
                    }

                    var processedFilesCount = processedFiles.Count;

                    processedFiles.Clear();

                    breaker.Success();

                    if (!stop && processedFilesCount == 0)
                    {
                        await Task.Delay(1000).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    await breaker.Failure(ex).ConfigureAwait(false);
                }

            } while (!stop);
        }
    }
}