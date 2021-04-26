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

                        var queueRuntimeInfos = await GetQueueList(token).ConfigureAwait(false);

                        Logger.DebugFormat("Retrieved details of {0} queues", queueRuntimeInfos.Count);

                        UpdateAllQueueLengths(queueRuntimeInfos);
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

        async Task<IReadOnlyDictionary<string, QueueRuntimeInfo>> GetQueueList(CancellationToken cancellationToken)
        {
            const int pageSize = 100; //This is the maximal page size for GetQueueAsync
            var pageNo = 0;

            var queuePathToRuntimeInfo = new Dictionary<string, QueueRuntimeInfo>(StringComparer.InvariantCultureIgnoreCase);

            while (true)
            {
                var pageOfQueueRuntimeInfo = await managementClient.GetQueuesRuntimeInfoAsync(count: pageSize, skip: pageNo * pageSize, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                foreach (var queueRuntimeInfo in pageOfQueueRuntimeInfo)
                {
                    queuePathToRuntimeInfo.Add(queueRuntimeInfo.Path, queueRuntimeInfo);
                }

                if (pageOfQueueRuntimeInfo.Count < pageSize)
                {
                    break;
                }

                pageNo++;
            }

            return queuePathToRuntimeInfo;
        }

        void UpdateAllQueueLengths(IReadOnlyDictionary<string, QueueRuntimeInfo> queues)
        {
            foreach (var eq in endpointQueueMappings)
            {
                UpdateQueueLength(eq, queues);
            }
        }

        void UpdateQueueLength(KeyValuePair<string, string> monitoredEndpoint, IReadOnlyDictionary<string, QueueRuntimeInfo> queues)
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