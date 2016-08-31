namespace ServiceControl.Operations
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Metrics;
    using NServiceBus;
    using NServiceBus.ObjectBuilder;
    using Raven.Abstractions.Commands;
    using Raven.Client;
    using ServiceControl.Contracts.MessageFailures;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.MessageFailures.Handlers;
    using ServiceControl.Operations.BodyStorage;

    class ProcessErrors
    {
        private const int BATCH_SIZE = 100;

        private readonly IDocumentStore store;
        private readonly StoreBody storeBody;
        private readonly IBus bus;
        private Task task;
        private ImportFailedMessageHandler failedMessageHandler;
        private volatile bool stop;
        private readonly Meter meter = Metric.Meter("Error messages processed", Unit.Results);

        public ProcessErrors(IBuilder builder, IDocumentStore store, StoreBody storeBody, IBus bus)
        {
            this.store = store;
            this.storeBody = storeBody;
            this.bus = bus;

            failedMessageHandler = new ImportFailedMessageHandler(builder.BuildAll<IFailedMessageEnricher>().ToArray(), builder.BuildAll<IEnrichImportedMessages>().ToArray());
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
                var patches = new List<PatchCommandData>();
                var events = new List<object>();
                var cnt = 0;

                foreach (var file in Directory.EnumerateFiles(storeBody.ErrorQueuePath))
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

                    var message = new ImportFailedMessage(transportMessage);

                    patches.Add(failedMessageHandler.Handle(message));

                    string failedMessageId;
                    if (message.PhysicalMessage.Headers.TryGetValue("ServiceControl.Retry.UniqueMessageId", out failedMessageId))
                    {
                        events.Add(new MessageFailedRepeatedly
                        {
                            FailureDetails = message.FailureDetails,
                            EndpointId = message.FailingEndpointId,
                            FailedMessageId = failedMessageId,
                        });
                    }
                    else
                    {
                        events.Add(new MessageFailed
                        {
                            FailureDetails = message.FailureDetails,
                            EndpointId = message.FailingEndpointId,
                            FailedMessageId = message.UniqueMessageId,
                        });
                    }

                    processedFiles.Add(file);

                    await storeBody.SaveToDB(message).ConfigureAwait(false);

                    if (cnt++ >= BATCH_SIZE)
                    {
                        break;
                    }
                }

                if (patches.Count > 0)
                {
                    await store.AsyncDatabaseCommands.BatchAsync(patches).ConfigureAwait(false);
                }

                if (events.Count > 0)
                {
                    Parallel.ForEach(events, e => bus.Publish(e));
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