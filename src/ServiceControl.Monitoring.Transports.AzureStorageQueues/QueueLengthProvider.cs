namespace ServiceControl.Transports.AzureStorageQueues
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Queue;
    using Monitoring;
    using Monitoring.Infrastructure;
    using Monitoring.Messaging;
    using Monitoring.QueueLength;
    using NServiceBus.Logging;
    using NServiceBus.Metrics;

    public class QueueLengthProvider
    {
        ConcurrentDictionary<EndpointInputQueue, QueueLengthValue> queueLengths = new ConcurrentDictionary<EndpointInputQueue, QueueLengthValue>();
        ConcurrentDictionary<string, string> problematicQueuesNames = new ConcurrentDictionary<string, string>();

        string connectionString;
        QueueLengthStore store;

        CancellationTokenSource stop = new CancellationTokenSource();
        Task poller;

        public void Initialize(string connectionString, QueueLengthStore store)
        {
            this.connectionString = connectionString;
            this.store = store;
        }

        public void Process(EndpointInstanceId endpointInstanceId, EndpointMetadataReport metadataReport)
        {
            var endpointInputQueue = new EndpointInputQueue(endpointInstanceId.EndpointName, metadataReport.LocalAddress);
            var queueName = QueueNameSanitizer.Sanitize(metadataReport.LocalAddress);

            var queueClient = CloudStorageAccount.Parse(connectionString).CreateCloudQueueClient();

            var emptyQueueLength = new QueueLengthValue
            {
                QueueName = queueName,
                Length = 0,
                QueueReference = queueClient.GetQueueReference(queueName)
            };

            queueLengths.AddOrUpdate(endpointInputQueue, _ => emptyQueueLength, (_, existingQueueLength) => existingQueueLength);
        }

        public void Process(EndpointInstanceId endpointInstanceId, TaggedLongValueOccurrence metricsReport)
        {
            //HINT: ASQ  server endpoints do not support endpoint level queue length monitoring
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
                        await FetchQueueSizes(token).ConfigureAwait(false);

                        UpdateQueueLengthStore();

                        await Task.Delay(QueryDelayInterval, token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // no-op
                    }
                    catch (Exception e)
                    {
                        Logger.Error("Error querying sql queue sizes.", e);
                    }
                }
            });

            return TaskEx.Completed;
        }

        public Task Stop()
        {
            stop.Cancel();

            return poller;
        }

        void UpdateQueueLengthStore()
        {
            var nowTicks = DateTime.UtcNow.Ticks;

            foreach (var endpointQueueLengthPair in queueLengths)
            {
                var queueLengthEntry = new RawMessage.Entry
                {
                    DateTicks = nowTicks,
                    Value = endpointQueueLengthPair.Value.Length
                };

                store.Store(new[]{ queueLengthEntry }, endpointQueueLengthPair.Key);
            }
        }

        Task FetchQueueSizes(CancellationToken token) => Task.WhenAll(queueLengths.Select(kvp => FetchLength(kvp.Value, token)));

        async Task FetchLength(QueueLengthValue queueLength, CancellationToken token)
        {
            try
            {
                var queueReference = queueLength.QueueReference;

                await queueReference.FetchAttributesAsync(token).ConfigureAwait(false);

                queueLength.Length = queueReference.ApproximateMessageCount.GetValueOrDefault();

                problematicQueuesNames.TryRemove(queueLength.QueueName, out _);
            }
            catch (OperationCanceledException)
            {
                // no-op
            }
            catch (Exception ex)
            {
                // simple "log once" approach to do not flood logs
                if (problematicQueuesNames.TryAdd(queueLength.QueueName, queueLength.QueueName))
                {
                    Logger.Error($"Obtaining Azure Storage Queue count failed for '{queueLength.QueueName}'", ex);
                }
            }
        }

        class QueueLengthValue
        {
            public string QueueName;
            public volatile int Length;
            public CloudQueue QueueReference;
        }

        static TimeSpan QueryDelayInterval = TimeSpan.FromMilliseconds(200);
        static ILog Logger = LogManager.GetLogger<QueueLengthProvider>();
    }
}