namespace ServiceControl.Transports.LearningTransport
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Logging;

    class QueueLengthProvider : IProvideQueueLength
    {
        public void Initialize(string connectionString, Action<QueueLengthEntry[], EndpointToQueueMapping> store)
        {
            rootFolder = connectionString;
            this.store = store;
        }

        public void TrackEndpointInputQueue(EndpointToQueueMapping queueToTrack)
        {
            endpointsHash.AddOrUpdate(queueToTrack, queueToTrack, (_, __) => queueToTrack);
        }

        public Task Start()
        {
            cancel = new CancellationTokenSource();
            task = Task.Run(async () =>
            {
                while (!cancel.IsCancellationRequested)
                {
                    try
                    {
                        var queueLengths = GetQueueLengths();
                        UpdateStore(queueLengths);

                        await Task.Delay(QueryDelayInterval, cancel.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // It's OK. We're shutting down
                    }
                    catch (Exception ex)
                    {
                        Log.Warn("Problem getting learning transport queue length", ex);
                    }
                }
            });

            return Task.FromResult(0);
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
                    store(new[]
                    {
                        new QueueLengthEntry
                        {
                            DateTicks = now,
                            Value = queueLength.Value
                        }
                    }, instance);
                }
                else
                {
                    Log.Warn($"Queue Length data missing for queue {instance.InputQueue} (Endpoint {instance.EndpointName})");
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

        public Task Stop()
        {
            cancel?.Cancel(false);
            return task;
        }

        string rootFolder;
        Action<QueueLengthEntry[], EndpointToQueueMapping> store;
        ConcurrentDictionary<EndpointToQueueMapping, EndpointToQueueMapping> endpointsHash = new ConcurrentDictionary<EndpointToQueueMapping, EndpointToQueueMapping>();
        CancellationTokenSource cancel;
        Task task;

        static TimeSpan QueryDelayInterval = TimeSpan.FromMilliseconds(200);
        static ILog Log = LogManager.GetLogger<QueueLengthProvider>();
    }
}