namespace ServiceControl.Operations
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Metrics;
    using NServiceBus;
    using NServiceBus.ObjectBuilder;
    using Raven.Client;
    using Raven.Client.Document;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.MessageAuditing.Handlers;
    using ServiceControl.Operations.BodyStorage;

    class ProcessAudits
    {
        private const int BATCH_SIZE = 100;

        private readonly IDocumentStore store;
        private readonly StoreBody storeBody;
        private Task task;
        private AuditMessageHandler auditMessageHandler;
        private volatile bool stop;
        private readonly Meter meter = Metric.Meter("Audit messages processed", Unit.Results);

        public ProcessAudits(IBuilder builder, IDocumentStore store, StoreBody storeBody)
        {
            this.store = store;
            this.storeBody = storeBody;

            auditMessageHandler = new AuditMessageHandler(store, builder.BuildAll<IEnrichImportedMessages>().ToArray());
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
                var processedFiles = new List<string>();
                var cnt = 0;

                var bulkInsertLazy = new Lazy<BulkInsertOperation>(() => store.BulkInsert());

                foreach (var file in Directory.EnumerateFiles(storeBody.AuditQueuePath))
                {
                    if (stop)
                    {
                        break;
                    }

                    Dictionary<string, string> headers;
                    byte[] body;
                    if (!storeBody.TryReadFile(file, out headers, out body))
                    {
                        continue;
                    }

                    var transportMessage = new TransportMessage(headers["NServiceBus.MessageId"], headers)
                    {
                        Body = body
                    };

                    var importSuccessfullyProcessedMessage = new ImportSuccessfullyProcessedMessage(transportMessage);
                    auditMessageHandler.Handle(bulkInsertLazy.Value, importSuccessfullyProcessedMessage);

                    processedFiles.Add(file);

                    await storeBody.SaveToDB(importSuccessfullyProcessedMessage).ConfigureAwait(false);

                    if (cnt++ >= BATCH_SIZE)
                    {
                        break;
                    }
                }

                if (cnt > 0)
                {
                    await bulkInsertLazy.Value.DisposeAsync();
                }

                if (processedFiles.Count > 0)
                {
                    Parallel.ForEach(processedFiles, File.Delete);
                }

                meter.Mark(cnt);

                if (!stop && cnt < BATCH_SIZE)
                {
                    await Task.Delay(1000);
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