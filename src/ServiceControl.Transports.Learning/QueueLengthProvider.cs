namespace ServiceControl.Transports.LearningTransport
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using ServiceControl.Transports.Learning;

    class QueueLengthProvider : AbstractQueueLengthProvider
    {
        public QueueLengthProvider(TransportSettings settings, Action<QueueLengthEntry[], EndpointToQueueMapping> store, ILogger<QueueLengthProvider> logger)
            : base(settings, store)
        {
            rootFolder = LearningTransportCustomization.FindStoragePath(ConnectionString);
            this.logger = logger;
        }

        public override void TrackEndpointInputQueue(EndpointToQueueMapping queueToTrack)
            => endpointsHash.AddOrUpdate(queueToTrack, queueToTrack, (_, __) => queueToTrack);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var queueLengths = GetQueueLengths();
                    UpdateStore(queueLengths);

                    await Task.Delay(QueryDelayInterval, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // It's OK. We're shutting down
                }
                catch (Exception ex)
                {
                    await Task.Delay(5000, stoppingToken);
                    logger.LogWarning(ex, "Problem getting learning transport queue length");
                }
            }
        }

        void UpdateStore(ILookup<string, long?> queueLengths)
        {
            var now = DateTime.UtcNow.Ticks;
            foreach (var kvp in endpointsHash)
            {
                var instance = kvp.Key;
                var queueLength = queueLengths[instance.InputQueue].FirstOrDefault();
                if (queueLength.HasValue)
                {
                    Store([
                        new QueueLengthEntry
                        {
                            DateTicks = now,
                            Value = queueLength.Value
                        }
                    ], instance);
                }
                else
                {
                    logger.LogWarning("Queue Length data missing for queue {InputQueue} (Endpoint {EndpointName})", instance.InputQueue, instance.EndpointName);
                }
            }
        }

        ILookup<string, long?> GetQueueLengths()
        {
            var result = new Dictionary<string, long>();

            var dirs = Directory.EnumerateDirectories(rootFolder);
            foreach (var dir in dirs)
            {
                var queueName = Path.GetFileName(dir);
                if (queueName != null)
                {
                    var queueLength = Directory.EnumerateFiles(dir).LongCount();
                    result[queueName] = queueLength;
                }
            }

            return result.ToLookup(x => x.Key, x => (long?)x.Value);
        }

        readonly string rootFolder;
        readonly ConcurrentDictionary<EndpointToQueueMapping, EndpointToQueueMapping> endpointsHash = new ConcurrentDictionary<EndpointToQueueMapping, EndpointToQueueMapping>();

        static readonly TimeSpan QueryDelayInterval = TimeSpan.FromMilliseconds(200);
        readonly ILogger<QueueLengthProvider> logger;

    }
}