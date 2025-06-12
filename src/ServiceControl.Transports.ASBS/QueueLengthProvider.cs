namespace ServiceControl.Transports.ASBS
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus.Administration;
    using Microsoft.Extensions.Logging;

    class QueueLengthProvider : AbstractQueueLengthProvider
    {
        public QueueLengthProvider(TransportSettings settings, Action<QueueLengthEntry[], EndpointToQueueMapping> store, ILogger<QueueLengthProvider> logger) : base(settings, store)
        {
            var connectionSettings = ConnectionStringParser.Parse(ConnectionString);

            queryDelayInterval = connectionSettings.QueryDelayInterval ?? TimeSpan.FromMilliseconds(500);

            managementClient = connectionSettings.AuthenticationMethod.BuildManagementClient();
            this.logger = logger;
        }

        public override void TrackEndpointInputQueue(EndpointToQueueMapping queueToTrack) =>
            endpointQueueMappings.AddOrUpdate(
                queueToTrack.InputQueue,
                id => queueToTrack.EndpointName,
                (id, old) => queueToTrack.EndpointName
            );

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    logger.LogDebug("Waiting for next interval");
                    await Task.Delay(queryDelayInterval, stoppingToken);

                    logger.LogDebug("Querying management client.");

                    var queueRuntimeInfos = await GetQueueList(stoppingToken);

                    logger.LogDebug("Retrieved details of {QueueCount} queues", queueRuntimeInfos.Count);

                    UpdateAllQueueLengths(queueRuntimeInfos);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // no-op
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Error querying Azure Service Bus queue sizes.");
                }
            }
        }

        async Task<IReadOnlyDictionary<string, QueueRuntimeProperties>> GetQueueList(CancellationToken cancellationToken)
        {
            var queuePathToRuntimeInfo = new Dictionary<string, QueueRuntimeProperties>(StringComparer.InvariantCultureIgnoreCase);

            var queuesRuntimeProperties = managementClient.GetQueuesRuntimePropertiesAsync(cancellationToken);
            var enumerator = queuesRuntimeProperties.GetAsyncEnumerator(cancellationToken);
            try
            {
                while (await enumerator.MoveNextAsync())
                {
                    var queueRuntimeProperties = enumerator.Current;
                    queuePathToRuntimeInfo[queueRuntimeProperties.Name] = queueRuntimeProperties; // Assuming last write is most up to date
                }
            }
            finally
            {
                await enumerator.DisposeAsync();
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

            Store(entries, new EndpointToQueueMapping(endpointName, queueName));
        }

        readonly ConcurrentDictionary<string, string> endpointQueueMappings = new ConcurrentDictionary<string, string>();
        readonly ServiceBusAdministrationClient managementClient;
        readonly TimeSpan queryDelayInterval;
        readonly ILogger<QueueLengthProvider> logger;
    }
}