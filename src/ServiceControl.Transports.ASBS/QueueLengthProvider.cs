namespace ServiceControl.Transports.ASBS
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.ServiceBus.Management;
    using NServiceBus.Logging;

    class QueueLengthProvider : IProvideQueueLength
    {
        public void Initialize(string connectionString, Action<QueueLengthEntry[], EndpointToQueueMapping> store)
        {
            var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };

            if (builder.TryGetValue(QueueLengthQueryIntervalPartName, out var value))
            {
                if (int.TryParse(value.ToString(), out var queryDelayInterval))
                {
                    QueryDelayInterval = TimeSpan.FromMilliseconds(queryDelayInterval);
                }
                else
                {
                    Logger.Warn($"Can't parse {value} as a valid query delay interval.");
                }
            }

            this.store = store;
            this.managementClient = new ManagementClient(connectionString);
        }

        public void TrackEndpointInputQueue(EndpointToQueueMapping queueToTrack)
        {
            endpointQueueMappings.AddOrUpdate(
                queueToTrack.InputQueue,
                id => queueToTrack.EndpointName,
                (id, old) => queueToTrack.EndpointName
            );
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

                        var queues = await GetQueueList(token).ConfigureAwait(false);

                        Logger.DebugFormat("Retrieved details of {0} queues", queues.Count);

                        var queuesLookup = new ConcurrentDictionary<string, QueueRuntimeInfo>(queues, StringComparer.InvariantCultureIgnoreCase);

                        UpdateAllQueueLengths(queuesLookup);
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

            return Task.CompletedTask;
        }

        async Task<Dictionary<string, QueueRuntimeInfo>> GetQueueList(CancellationToken token)
        {
            var pageSize = 100; //This is the maximal page size for GetQueueAsync
            var pageNo = 0;

            var queues = new List<QueueRuntimeInfo>();

            while (true)
            {
                //var page = await managementClient.GetQueuesAsync(count: pageSize, skip: pageNo * pageSize, cancellationToken: token).ConfigureAwait(false);
                var pages = await managementClient.GetQueuesRuntimeInfoAsync(count: pageSize, skip: pageNo * pageSize, cancellationToken: token).ConfigureAwait(false);

                queues.AddRange(pages);

                if (pages.Count < pageSize)
                {
                    break;
                }

                pageNo++;
            }

            return queues.ToDictionary(q => q.Path, q => q);
        }

        void UpdateAllQueueLengths(ConcurrentDictionary<string, QueueRuntimeInfo> queues)
        {
            foreach (var eq in endpointQueueMappings)
            {
                UpdateQueueLength(eq, queues);
            }
        }

        void UpdateQueueLength(KeyValuePair<string, string> monitoredEndpoint, ConcurrentDictionary<string, QueueRuntimeInfo> queues)
        {
            var endpointName = monitoredEndpoint.Value;
            var queueName = monitoredEndpoint.Key;

            if (!queues.TryGetValue(queueName, out var runtimeInfo))
            {
                return;
            }

            var entries = new[]
            {
                new QueueLengthEntry
                {
                    DateTicks =  DateTime.UtcNow.Ticks,
                    Value = runtimeInfo.MessageCountDetails.ActiveMessageCount
                }
            };

            store(entries, new EndpointToQueueMapping(endpointName, queueName));
        }

        public async Task Stop()
        {
            stop.Cancel();
            await poller.ConfigureAwait(false);
            await managementClient.CloseAsync().ConfigureAwait(false);
        }

        ConcurrentDictionary<string, string> endpointQueueMappings = new ConcurrentDictionary<string, string>();
        Action<QueueLengthEntry[], EndpointToQueueMapping> store;
        ManagementClient managementClient;
        CancellationTokenSource stop = new CancellationTokenSource();
        Task poller;

        static TimeSpan QueryDelayInterval = TimeSpan.FromMilliseconds(500);
        static string QueueLengthQueryIntervalPartName = "QueueLengthQueryDelayInterval";
        static ILog Logger = LogManager.GetLogger<QueueLengthProvider>();
    }
}