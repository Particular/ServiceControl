namespace ServiceControl.Operations
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Metrics;
    using NServiceBus;
    using NServiceBus.ObjectBuilder;
    using Raven.Client;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.MessageAuditing.Handlers;
    using ServiceControl.Operations.BodyStorage;

    class ProcessAudits
    {
        private readonly IDocumentStore store;
        private readonly StoreBody storeBody;
        private Task task;
        private AuditMessageHandler auditMessageHandler;
        private volatile bool stop;
        private readonly Timer timer = Metric.Timer("Audit messages processed", Unit.Requests);

        public ProcessAudits(IBuilder builder, IDocumentStore store, StoreBody storeBody)
        {
            this.store = store;
            this.storeBody = storeBody;

            auditMessageHandler = new AuditMessageHandler(store, builder.BuildAll<IEnrichImportedMessages>().ToArray());
        }

        public void Start()
        {
            stop = false;
            task = Task.Run(Process);
        }

        private const int BATCH_SIZE = 100;

        private async Task Process()
        {
            do
            {
                var processedFiles = new List<string>();
                var cnt = 0;

                using (timer.NewContext())
                {
                    using (var bulkInsert = store.BulkInsert())
                    {
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
                                Body = body,
                            };

                            var importSuccessfullyProcessedMessage = new ImportSuccessfullyProcessedMessage(transportMessage);
                            auditMessageHandler.Handle(bulkInsert, importSuccessfullyProcessedMessage);

                            processedFiles.Add(file);

                            await storeBody.SaveToDB(importSuccessfullyProcessedMessage);

                            if (cnt++ >= BATCH_SIZE)
                            {
                                break;
                            }
                        }
                    }


                    foreach (var processedFile in processedFiles)
                    {
                        File.Delete(processedFile);
                    }
                }
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