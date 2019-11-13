namespace ServiceControl.Transports.ASQ
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Collections.Concurrent;
    using System.Threading;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Queue;
    using NServiceBus.Logging;

    class QueueLengthProvider : IProvideQueueLength
    {
        public void Initialize(string connectionString, QueueLengthStoreDto storeDto)
        {
            this.connectionString = connectionString;
            store = storeDto;
        }

        public void Process(EndpointInstanceIdDto endpointInstanceIdDto, string queueAddress)
        {
            var endpointInputQueue = new EndpointInputQueueDto(endpointInstanceIdDto.EndpointName, queueAddress);

            var queueName = BackwardsCompatibleQueueNameSanitizer.Sanitize(queueAddress);

            var queueClient = CloudStorageAccount.Parse(connectionString).CreateCloudQueueClient();

            var emptyQueueLength = new QueueLengthValue
            {
                QueueName = queueName,
                Length = 0,
                QueueReference = queueClient.GetQueueReference(queueName)
            };

            queueLengths.AddOrUpdate(endpointInputQueue, _ => emptyQueueLength, (_, existingQueueLength) => existingQueueLength);
        }

        public void Process(EndpointInstanceIdDto endpointInstanceIdDto, TaggedLongValueOccurrenceDto metricsReport)
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

            return Task.CompletedTask;
        }

        public Task Stop()
        {
            stop.Cancel();

            return poller;
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

        void UpdateQueueLengthStore()
        {
            var nowTicks = DateTime.UtcNow.Ticks;

            foreach (var endpointQueueLengthPair in queueLengths)
            {
                var queueLengthEntry = new EntryDto
                {
                    DateTicks = nowTicks,
                    Value = endpointQueueLengthPair.Value.Length
                };

                store.Store(new[] { queueLengthEntry }, endpointQueueLengthPair.Key);
            }
        }

        string connectionString;
        QueueLengthStoreDto store;

        CancellationTokenSource stop = new CancellationTokenSource();
        Task poller;

        ConcurrentDictionary<EndpointInputQueueDto, QueueLengthValue> queueLengths = new ConcurrentDictionary<EndpointInputQueueDto, QueueLengthValue>();
        ConcurrentDictionary<string, string> problematicQueuesNames = new ConcurrentDictionary<string, string>();

        static ILog Logger = LogManager.GetLogger<QueueLengthProvider>();
        static TimeSpan QueryDelayInterval = TimeSpan.FromMilliseconds(200);

        class QueueLengthValue
        {
            public string QueueName;
            public volatile int Length;
            public CloudQueue QueueReference;
        }
    }
}
