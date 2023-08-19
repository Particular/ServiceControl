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

                    // Assuming last write is most up to date
                    if (queuePathToRuntimeInfo.ContainsKey(queueRuntimeProperties.Name))
                    {
                        var existingItem = queuePathToRuntimeInfo[queueRuntimeProperties.Name];
                        if (existingItem != null)
                        {
                            queuePathToRuntimeInfo[queueRuntimeProperties.Name] = await CompareQueueDates(existingItem, queueRuntimeProperties).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        queuePathToRuntimeInfo[queueRuntimeProperties.Name] = queueRuntimeProperties;
                    }
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

        async Task<QueueRuntimeProperties> CompareQueueDates(QueueRuntimeProperties existingItem, QueueRuntimeProperties queueRuntimeProperty)
        {
            var createdAtCompare = (TimeComparison)DateTimeOffset.Compare(existingItem.CreatedAt, queueRuntimeProperty.CreatedAt);
            var updatedAtCompare = (TimeComparison)DateTimeOffset.Compare(existingItem.UpdatedAt, queueRuntimeProperty.UpdatedAt);
            var accessedAtCompare = (TimeComparison)DateTimeOffset.Compare(existingItem.AccessedAt, queueRuntimeProperty.AccessedAt);

            if (createdAtCompare == TimeComparison.Earlier)
            {
                Logger.WarnFormat("Queue <{0}> already processed with an older 'createdAt' date: {1}.  Queue is being updated with newer properties that has the 'createdAt' date: {2}", existingItem.Name, existingItem.CreatedAt, queueRuntimeProperty.CreatedAt);
                return await Task.FromResult(queueRuntimeProperty).ConfigureAwait(false);
            }
            else if (createdAtCompare == TimeComparison.Later)
            {
                Logger.WarnFormat("Queue <{0}> already processed with a newer 'createdAt' date: {1}. The duplicate queue with the 'createdAt' date: {2} is being discarded.", existingItem.Name, existingItem.CreatedAt, queueRuntimeProperty.CreatedAt);
            }
            else if (updatedAtCompare == TimeComparison.Earlier)
            {
                Logger.WarnFormat("Queue <{0}> already processed with an older 'updatedAt' date: {1}. Queue is being updated with newer properties that has the 'updatedAt' date: {2}", existingItem.Name, existingItem.UpdatedAt, queueRuntimeProperty.UpdatedAt);
                return await Task.FromResult(queueRuntimeProperty).ConfigureAwait(false);
            }
            else if (updatedAtCompare == TimeComparison.Later)
            {
                Logger.WarnFormat("Queue <{0}> already processed with a newer 'updatedAt' date: {1}. The duplicate queue with the 'updatedAt' date: {2} is being discarded.", existingItem.Name, existingItem.UpdatedAt, queueRuntimeProperty.UpdatedAt);
            }
            else if (accessedAtCompare == TimeComparison.Earlier)
            {
                Logger.WarnFormat("Queue <{0}> already processed with an older 'accessedAt' date: {1}. Queue is being updated with newer properties that has the 'accessedAt' date: {2}", existingItem.Name, existingItem.AccessedAt, queueRuntimeProperty.AccessedAt);
                return await Task.FromResult(queueRuntimeProperty).ConfigureAwait(false);
            }
            else if (accessedAtCompare == TimeComparison.Later)
            {
                Logger.WarnFormat("Queue <{0}> already processed with a newer 'accessedAt' date: {1}. The duplicate queue with the 'accessedAt' date: {2} is being discarded.", existingItem.Name, existingItem.AccessedAt, queueRuntimeProperty.AccessedAt);
            }
            else
            {
                Logger.WarnFormat("Queue <{0}> already processed. The duplicate queue is being discarded.", existingItem.Name);
            }

            return await Task.FromResult(existingItem).ConfigureAwait(false);
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

        enum TimeComparison
        {
            Earlier = -1,
            Same = 0,
            Later = 1
        }
    }
}