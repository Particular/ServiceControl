namespace ServiceControl.Transports.RabbitMQ
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Logging;
    using NServiceBus.Transport.RabbitMQ.ManagementApi;

    class QueueLengthProvider : AbstractQueueLengthProvider
    {
        public QueueLengthProvider(TransportSettings settings, Action<QueueLengthEntry[], EndpointToQueueMapping> store, ITransportCustomization transportCustomization) : base(settings, store)
        {
            if (transportCustomization is IManagementClientProvider provider)
            {
                managementClient = provider.GetManagementClient();
            }
            else
            {
                throw new ArgumentException($"Transport customization does not implement {nameof(IManagementClientProvider)}. Type: {transportCustomization.GetType().Name}", nameof(transportCustomization));
            }
        }

        public override void TrackEndpointInputQueue(EndpointToQueueMapping queueToTrack) =>
            endpointQueues.AddOrUpdate(queueToTrack.EndpointName, _ => queueToTrack.InputQueue, (_, currentValue) =>
            {
                if (currentValue != queueToTrack.InputQueue)
                {
                    sizes.TryRemove(currentValue, out var _);
                }

                return queueToTrack.InputQueue;
            });

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await FetchQueueLengths(stoppingToken);

                    UpdateQueueLengths();

                    await Task.Delay(QueryDelayInterval, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // no-op
                }
                catch (Exception e)
                {
                    Logger.Error("Queue length query loop failure.", e);
                }
            }
        }

        void UpdateQueueLengths()
        {
            var nowTicks = DateTime.UtcNow.Ticks;

            foreach (var endpointQueuePair in endpointQueues)
            {
                if (sizes.TryGetValue(endpointQueuePair.Value, out var size))
                {
                    Store(
                        [
                            new QueueLengthEntry
                            {
                                DateTicks = nowTicks,
                                Value = size
                            }
                        ],
                        new EndpointToQueueMapping(endpointQueuePair.Key, endpointQueuePair.Value));
                }
            }
        }

        async Task FetchQueueLengths(CancellationToken cancellationToken)
        {
            foreach (var endpointQueuePair in endpointQueues)
            {
                var queueName = endpointQueuePair.Value;

                try
                {
                    var queue = await managementClient.Value.GetQueue(queueName, cancellationToken);

                    var size = queue.MessageCount;
                    sizes.AddOrUpdate(queueName, _ => size, (_, _) => size);
                }
                catch (Exception e)
                {
                    Logger.Warn($"Error querying queue length for {queueName}", e);
                }
            }
        }

        static readonly TimeSpan QueryDelayInterval = TimeSpan.FromMilliseconds(200);

        readonly ConcurrentDictionary<string, string> endpointQueues = new();
        readonly ConcurrentDictionary<string, long> sizes = new();

        static readonly ILog Logger = LogManager.GetLogger<QueueLengthProvider>();

        readonly Lazy<ManagementClient> managementClient;
    }
}
