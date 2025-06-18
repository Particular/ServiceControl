namespace ServiceControl.Transports.ASQ
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Storage.Queues;
    using Azure.Storage.Queues.Models;
    using Microsoft.Extensions.Logging;

    class QueueLengthProvider : AbstractQueueLengthProvider
    {
        public QueueLengthProvider(TransportSettings settings, Action<QueueLengthEntry[], EndpointToQueueMapping> store, ILogger<QueueLengthProvider> logger)
            : base(settings, store)
        {
            connectionString = ConnectionString.RemoveCustomConnectionStringParts(out _);
            this.logger = logger;
        }

        public override void TrackEndpointInputQueue(EndpointToQueueMapping queueToTrack)
        {
            var queueName = BackwardsCompatibleQueueNameSanitizer.Sanitize(queueToTrack.InputQueue);
            var queueClient = new QueueClient(connectionString, queueName);

            var emptyQueueLength = new QueueLengthValue
            {
                QueueName = queueName,
                Length = 0,
                QueueReference = queueClient
            };

            queueLengths.AddOrUpdate(queueToTrack, _ => emptyQueueLength, (_, existingQueueLength) => existingQueueLength);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await FetchQueueSizes(stoppingToken);

                    UpdateQueueLengthStore();

                    await Task.Delay(QueryDelayInterval, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // no-op
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Error querying sql queue sizes.");
                }
            }
        }

        Task FetchQueueSizes(CancellationToken cancellationToken) => Task.WhenAll(queueLengths.Select(kvp => FetchLength(kvp.Value, cancellationToken)));

        async Task FetchLength(QueueLengthValue queueLength, CancellationToken cancellationToken)
        {
            try
            {
                var queueReference = queueLength.QueueReference;

                QueueProperties properties = await queueReference.GetPropertiesAsync(cancellationToken);

                queueLength.Length = properties.ApproximateMessagesCount;

                problematicQueuesNames.TryRemove(queueLength.QueueName, out _);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // no-op
            }
            catch (Exception ex)
            {
                // simple "log once" approach to do not flood logs
                if (problematicQueuesNames.TryAdd(queueLength.QueueName, queueLength.QueueName))
                {
                    logger.LogError(ex, "Obtaining Azure Storage Queue count failed for '{QueueName}'", queueLength.QueueName);
                }
            }
        }

        void UpdateQueueLengthStore()
        {
            var nowTicks = DateTime.UtcNow.Ticks;

            foreach (var endpointQueueLengthPair in queueLengths)
            {
                var queueLengthEntry = new QueueLengthEntry
                {
                    DateTicks = nowTicks,
                    Value = endpointQueueLengthPair.Value.Length
                };

                Store(new[] { queueLengthEntry }, endpointQueueLengthPair.Key);
            }
        }

        readonly string connectionString;

        readonly ConcurrentDictionary<EndpointToQueueMapping, QueueLengthValue> queueLengths = new ConcurrentDictionary<EndpointToQueueMapping, QueueLengthValue>();
        readonly ConcurrentDictionary<string, string> problematicQueuesNames = new ConcurrentDictionary<string, string>();

        readonly ILogger<QueueLengthProvider> logger;
        static readonly TimeSpan QueryDelayInterval = TimeSpan.FromMilliseconds(200);

        class QueueLengthValue
        {
            public string QueueName;
            public volatile int Length;
            public QueueClient QueueReference;
        }
    }
}
