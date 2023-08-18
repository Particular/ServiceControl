namespace ServiceControl.Transports.ASBS
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus.Administration;
    using NServiceBus.Logging;

    class QueueLengthProvider : IProvideQueueLength
    {
        public void Initialize(string connectionString, Action<QueueLengthEntry[], EndpointToQueueMapping> store)
        {
            this.store = store;

            var connectionSettings = ConnectionStringParser.Parse(connectionString);

            if (connectionSettings.QueryDelayInterval.HasValue)
            {
                queryDelayInterval = connectionSettings.QueryDelayInterval.Value;
            }
            else
            {
                queryDelayInterval = TimeSpan.FromMilliseconds(500);
            }

            managementClient = connectionSettings.AuthenticationMethod.BuildManagementClient();
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
                        await Task.Delay(queryDelayInterval, token).ConfigureAwait(false);

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

        async Task<IReadOnlyDictionary<string, QueueRuntimeProperties>> GetQueueList(CancellationToken cancellationToken)
        {
            var queuePathToRuntimeInfo = new Dictionary<string, QueueRuntimeProperties>(StringComparer.InvariantCultureIgnoreCase);

            var queuesRuntimeProperties = managementClient.GetQueuesRuntimePropertiesAsync(cancellationToken);
            var enumerator = queuesRuntimeProperties.GetAsyncEnumerator(cancellationToken);
            try
            {
                while (await enumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    var queueRuntimeProperties = enumerator.Current;
                    if (queuePathToRuntimeInfo.ContainsKey(queueRuntimeProperties.Name))
                    {
                        var existingItem = queuePathToRuntimeInfo[queueRuntimeProperties.Name];
                        if(existingItem.Count!=queueRuntimeProperties.Count)
                        {
                            Logger.WarnFormat("Queue <{0}> already processed with different length (length {1} vs {2})", queueRuntimeProperties.Name, queuePathToRuntimeInfo.Count, existingItem.Count);
                        }
                    }
                    queuePathToRuntimeInfo[queueRuntimeProperties.Name] = queueRuntimeProperties; // Assuming last write is most up to date
                }
            }
            finally
            {
                await enumerator.DisposeAsync().ConfigureAwait(false);
            }

            return queuePathToRuntimeInfo;
        }

        void UpdateAllQueueLengths(IReadOnlyDictionary<string, QueueRuntimeProperties> queues)
        {
            foreach (var eq in endpointQueueMappings)
            {
                UpdateQueueLength(eq, queues);
            }
        }

        void UpdateQueueLength(KeyValuePair<string, string> monitoredEndpoint, IReadOnlyDictionary<string, QueueRuntimeProperties> queues)
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
                    Value = runtimeInfo.ActiveMessageCount
                }
            };

            store(entries, new EndpointToQueueMapping(endpointName, queueName));
        }

        public async Task Stop()
        {
            stop.Cancel();
            await poller.ConfigureAwait(false);
        }

        ConcurrentDictionary<string, string> endpointQueueMappings = new ConcurrentDictionary<string, string>();
        Action<QueueLengthEntry[], EndpointToQueueMapping> store;
        ServiceBusAdministrationClient managementClient;
        CancellationTokenSource stop = new CancellationTokenSource();
        Task poller;
        TimeSpan queryDelayInterval;

        static ILog Logger = LogManager.GetLogger<QueueLengthProvider>();
    }
}