namespace ServiceControl.Transports.AzureServiceBus
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.ServiceBus.Management;
    using Monitoring;
    using Monitoring.Infrastructure;
    using Monitoring.Messaging;
    using Monitoring.QueueLength;
    using NServiceBus.Logging;
    using NServiceBus.Metrics;

    public class QueueLengthProvider : IProvideQueueLength
    {
        ConcurrentDictionary<EndpointInstanceId, string> endpointQueueMappings = new ConcurrentDictionary<EndpointInstanceId, string>();

        QueueLengthStore queueLengthStore;
        ManagementClient managementClient;

        CancellationTokenSource stop = new CancellationTokenSource();
        Task poller;

        public void Initialize(string connectionString, QueueLengthStore store)
        {
            this.queueLengthStore = store;
            this.managementClient = new ManagementClient(connectionString);
        }

        public void Process(EndpointInstanceId endpointInstanceId, EndpointMetadataReport metadataReport)
        {
            endpointQueueMappings.AddOrUpdate(
                endpointInstanceId,
                id => metadataReport.LocalAddress,
                (id, old) => metadataReport.LocalAddress
            );
        }

        public void Process(EndpointInstanceId endpointInstanceId, TaggedLongValueOccurrence metricsReport)
        {
        }

        public Task Start()
        {
            stop = new CancellationTokenSource();

            poller = Task.Run(async () =>
            {
                var token = stop.Token;

                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        Logger.Debug("Waiting for next interval");
                        await Task.Delay(QueryDelayInterval, token).ConfigureAwait(false);

                        Logger.DebugFormat("Querying management client.");

                        var queues = await managementClient.GetQueuesAsync(cancellationToken: token).ConfigureAwait(false);
                        var lookup = queues.ToLookup(x => x.Path, StringComparer.InvariantCultureIgnoreCase);

                        Logger.DebugFormat("Retrieved details of {0} queues", lookup.Count);

                        await UpdateQueueLengthStore(lookup, token);
                    }
                    catch (OperationCanceledException)
                    {
                        // no-op
                    }
                    catch (Exception e)
                    {
                        Logger.Error("Error querying Azure Service Bus queue sizes.", e);
                    }
                }
            });

            return TaskEx.Completed;
        }

        async Task UpdateQueueLengthStore(ILookup<string, QueueDescription> queueData, CancellationToken token)
        {
            var timestamp = DateTime.UtcNow.Ticks;
            foreach (var mapping in endpointQueueMappings)
            {
                var queue = queueData[mapping.Value].FirstOrDefault();
                if (queue != null)
                {
                    var entries = new[]
                    {
                        new RawMessage.Entry
                        {
                            DateTicks = timestamp,
                            Value = (await managementClient.GetQueueRuntimeInfoAsync(queue.Path, token).ConfigureAwait(false)).MessageCountDetails.ActiveMessageCount
                        }
                    };
                    queueLengthStore.Store(entries, new EndpointInputQueue(mapping.Key.EndpointName, queue.Path));
                }
                else
                {
                    Logger.DebugFormat("Endpoint {0} ({1}): no queue length data found for queue {2}", mapping.Key.EndpointName, mapping.Key.InstanceName ?? mapping.Key.InstanceId, mapping.Value);
                }
            }
        }

        public async Task Stop()
        {
            stop.Cancel();
            await poller.ConfigureAwait(false);
            await managementClient.CloseAsync().ConfigureAwait(false);
        }

        static TimeSpan QueryDelayInterval = TimeSpan.FromMilliseconds(200);

        static ILog Logger = LogManager.GetLogger<QueueLengthProvider>();
    }
}