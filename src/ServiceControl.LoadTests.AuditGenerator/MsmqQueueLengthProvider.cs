using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Messaging;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.Logging;
using ServiceControl.Transports;

namespace ServiceControl.LoadTests.AuditGenerator
{
    public class MsmqQueueLengthProvider : IProvideQueueLength
    {
        public void Initialize(string connectionString, Action<QueueLengthEntry[], EndpointToQueueMapping> store)
        {
            callback = store;
        }

        public void TrackEndpointInputQueue(EndpointToQueueMapping queueToTrack)
        {
            endpointsHash.AddOrUpdate(queueToTrack, CreateQueue, (mapping, queue) => queue ?? CreateQueue(mapping));
        }

        MessageQueue CreateQueue(EndpointToQueueMapping mapping)
        {
            var queueName = mapping.InputQueue.Split('@').FirstOrDefault();

            var messageQueue = new MessageQueue($@".\private$\{queueName}", QueueAccessMode.Peek);

            return messageQueue;
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
                        UpdateStore();

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

        void UpdateStore()
        {
            var now = DateTime.UtcNow.Ticks;
            foreach (var kvp in endpointsHash)
            {
                var instance = kvp.Key;
                var queue = kvp.Value;
                var queueLength = queue.GetCount();

                callback(new[]
                {
                    new QueueLengthEntry
                    {
                        DateTicks = now,
                        Value = queueLength
                    }
                }, instance);
            }
        }

        public Task Stop()
        {
            cancel?.Cancel(false);
            return task;
        }

        Action<QueueLengthEntry[], EndpointToQueueMapping> callback;
        readonly ConcurrentDictionary<EndpointToQueueMapping, MessageQueue> endpointsHash = new ConcurrentDictionary<EndpointToQueueMapping, MessageQueue>();
        CancellationTokenSource cancel;
        Task task;

        static readonly TimeSpan QueryDelayInterval = TimeSpan.FromMilliseconds(1000);
        static readonly ILog Log = LogManager.GetLogger<MsmqQueueLengthProvider>();
    }
}