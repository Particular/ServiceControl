namespace ServiceControl.Transports.ASBS
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus.Administration;
    using NServiceBus.Logging;

    class QueueLengthProvider : AbstractQueueLengthProvider
    {
        public QueueLengthProvider(TransportSettings settings, Action<QueueLengthEntry[], EndpointToQueueMapping> store) : base(settings, store)
        {
            var connectionSettings = ConnectionStringParser.Parse(ConnectionString);

            queryDelayInterval = connectionSettings.QueryDelayInterval ?? TimeSpan.FromMilliseconds(500);

            managementClient = connectionSettings.AuthenticationMethod.BuildManagementClient();
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
                    Logger.Debug("Waiting for next interval");
                    await Task.Delay(queryDelayInterval, stoppingToken);

                    Logger.DebugFormat("Querying management client.");

                    var queueRuntimeInfos = await GetQueueList(stoppingToken);

                    Logger.DebugFormat("Retrieved details of {0} queues", queueRuntimeInfos.Count);

                    UpdateAllQueueLengths(queueRuntimeInfos);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // no-op
                }
                catch (Exception e)
                {
                    Logger.Error("Error querying Azure Service Bus queue sizes.", e);
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

        static readonly ILog Logger = LogManager.GetLogger<QueueLengthProvider>();
    }
}