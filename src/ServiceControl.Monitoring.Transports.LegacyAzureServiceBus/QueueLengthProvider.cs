namespace ServiceControl.Transports.LegacyAzureServiceBus
{
    using System;
    using System.Collections.Concurrent;
    using System.Data.Common;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.ServiceBus;
    using Microsoft.ServiceBus.Messaging;
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
        NamespaceManager namespaceManager;

        CancellationTokenSource stop = new CancellationTokenSource();
        Task poller;

        public void Initialize(string connectionString, QueueLengthStore store)
        {
            var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };

            if (builder.TryGetValue(QueueLengthQueryIntervalPartName, out var value) )
            {
                if (int.TryParse(value.ToString(), out var queryDelayInterval))
                {
                    QueryDelayInterval = TimeSpan.FromMilliseconds(queryDelayInterval);
                }
                else
                {
                    Logger.Warn($"Can't parse {value} as a valid query delay interval.");
                }

                //If the custom part stays in the connection string and is at the end, the sdk will treat is as part of the SharedAccessKey
                connectionString = ConnectionStringPartRemover.Remove(connectionString, QueueLengthQueryIntervalPartName);
            }

            this.queueLengthStore = store;
            this.namespaceManager = NamespaceManager.CreateFromConnectionString(connectionString);
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
                        Logger.DebugFormat("Querying namespace manager: {0}", namespaceManager.Address);

                        var queues = await namespaceManager.GetQueuesAsync().ConfigureAwait(false);
                        var lookup = queues.ToLookup(x => x.Path, StringComparer.InvariantCultureIgnoreCase);

                        Logger.DebugFormat("Retrieved details of {0} queues", lookup.Count);

                        UpdateQueueLengthStore(lookup);

                        Logger.Debug("Waiting for next interval");
                        await Task.Delay(QueryDelayInterval, token).ConfigureAwait(false);
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

        private void UpdateQueueLengthStore(ILookup<string, QueueDescription> queueData)
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
                            Value = queue.MessageCountDetails.ActiveMessageCount
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

        public Task Stop()
        {
            stop.Cancel();

            return poller;
        }

        static TimeSpan QueryDelayInterval = TimeSpan.FromMilliseconds(500);
        static ILog Logger = LogManager.GetLogger<QueueLengthProvider>();
        
        public static string QueueLengthQueryIntervalPartName = "QueueLengthQueryDelayInterval";
    }
}