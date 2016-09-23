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
        private const int BATCH_SIZE = 128;
        private const int NUM_CONCURRENT_BATCHES = 5;

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
            do
            {
                var tasks = new List<Task>(NUM_CONCURRENT_BATCHES);

                foreach (var files in Ext.Chunk(auditIngestionCache.GetBatch(BATCH_SIZE * NUM_CONCURRENT_BATCHES), BATCH_SIZE))
                {
                    tasks.Add(Task.Run(() => HandleBatch(files)));
                }
                
                if (tasks.Count > 0)
                {
                    await Task.WhenAll(tasks).ConfigureAwait(false);
                }

                if (!stop && tasks.Count == 0)
                {
                    await Task.Delay(1000).ConfigureAwait(false);
                }
                
            } while (!stop);
        }

        private async Task HandleBatch(string[] files)
        {
            try
            {
                var commit = false;
                BulkInsertOperation bulkInsert = null;
                try
                {
                    bulkInsert = store.BulkInsert(options: new BulkInsertOptions
                    {
                        WriteTimeoutMilliseconds = 2000
                    });

                    for (var index = 0; index < files.Length; index++)
                    {
                        Dictionary<string, string> headers;
                        ClaimsCheck bodyStorageClaimsCheck;

                        if (auditIngestionCache.TryGet(files[index], out headers, out bodyStorageClaimsCheck))
                        {
                            var processedMessage = processedMessageFactory.Create(headers);
                            processedMessageFactory.AddBodyDetails(processedMessage, bodyStorageClaimsCheck);

                            bulkInsert.Store(processedMessage);

                            commit = true;
                        }
                        else
                        {
                            files[index] = null;
                        }
                    }
                }
                finally
                {
                    if (bulkInsert != null)
                    {
                        await bulkInsert.DisposeAsync().ConfigureAwait(false);
                    }
                }

                var processedFiles = 0;
                if (commit)
                {
                    foreach (var file in files)
                    {
                        if (file != null)
                        {
                            File.Delete(file);
                            processedFiles++;
                        }
                    }
                }

                meter.Mark(processedFiles);

                breaker.Success();
            }
            catch (Exception ex)
            {
                await breaker.Failure(ex).ConfigureAwait(false);
            }
        }
    }

    public static class Ext
    {
        public static IEnumerable<T[]> Chunk<T>(IEnumerable<T> source, int batchSize)
        {
            var list = new List<T>(batchSize);
            var i = 0;
            foreach (var item in source)
            {
                list.Add(item);
                if (++i == batchSize)
                {
                    yield return list.ToArray();
                    i = 0;
                    list.Clear();
                }
            }

            if (i != 0)
            {
                yield return list.ToArray();
            }
        }
    }
}