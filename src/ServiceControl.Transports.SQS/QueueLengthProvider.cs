namespace ServiceControl.Transports.SQS
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Data.Common;
    using System.Collections.Concurrent;
    using System.Threading;
    using Amazon.Runtime;
    using Amazon.SQS;
    using NServiceBus.Logging;

    class QueueLengthProvider : IProvideQueueLength
    {
        public void Initialize(string connectionString, Action<QueueLengthEntry[], EndpointToQueueMapping> storeDto)
        {
            var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
            if (builder.ContainsKey("AccessKeyId") || builder.ContainsKey("SecretAccessKey"))
            {
                // if the user provided the access key and secret access key they should always be loaded from environment credentials
                clientFactory = () => new AmazonSQSClient(new EnvironmentVariablesAWSCredentials());
            }
            else
            {
                //See https://docs.aws.amazon.com/sdk-for-net/v3/developer-guide/net-dg-config-creds.html#creds-assign
                Logger.Info("BasicAWSCredentials have not been supplied in the connection string. Attempting to use existing environment or IAM role credentials.");
            }

            if (builder.TryGetValue("QueueNamePrefix", out var prefix))
            {
                queueNamePrefix = (string)prefix;

            }
            store = storeDto;
        }

        public void TrackEndpointInputQueue(EndpointToQueueMapping queueToTrack)
        {
            var queue = QueueNameHelper.GetSqsQueueName(queueToTrack.InputQueue, queueNamePrefix);

            queues.AddOrUpdate(queueToTrack, _ => queue, (_, currentQueue) =>
            {
                if (currentQueue != queue)
                {
                    sizes.TryRemove(currentQueue, out var _);
                }

                return queue;
            });

            sizes.TryAdd(queue, 0);
        }

        public Task Start()
        {
            stop = new CancellationTokenSource();

            poller = Task.Run(async () =>
            {
                using (var client = clientFactory())
                {
                    var cache = new QueueAttributesRequestCache(client);
                    var token = stop.Token;

                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            await FetchQueueSizes(cache, client, token).ConfigureAwait(false);

                            UpdateQueueLengthStore();

                            await Task.Delay(QueryDelayInterval, token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            // no-op
                        }
                        catch (Exception e)
                        {
                            Logger.Error("Error querying SQS queue sizes.", e);
                        }
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

        void UpdateQueueLengthStore()
        {
            var nowTicks = DateTime.UtcNow.Ticks;

            foreach (var tableNamePair in queues)
            {
                store(
                    new[]
                    {
                        new QueueLengthEntry
                        {
                            DateTicks = nowTicks,
                            Value = sizes.TryGetValue(tableNamePair.Value, out var size) ? size : 0
                        }
                    },
                    tableNamePair.Key);
            }
        }

        Task FetchQueueSizes(QueueAttributesRequestCache cache, IAmazonSQS client, CancellationToken cancellationToken) => Task.WhenAll(sizes.Select(kvp => FetchLength(kvp.Key, client, cache, cancellationToken)));

        async Task FetchLength(string queue, IAmazonSQS client, QueueAttributesRequestCache cache, CancellationToken cancellationToken)
        {
            try
            {
                var attReq = await cache.GetQueueAttributesRequest(queue, cancellationToken).ConfigureAwait(false);
                var response = await client.GetQueueAttributesAsync(attReq, cancellationToken).ConfigureAwait(false);
                sizes[queue] = response.ApproximateNumberOfMessages;
            }
            catch (OperationCanceledException)
            {
                // no-op
            }
            catch (Exception ex)
            {
                Logger.Error($"Obtaining an approximate number of messages failed for '{queue}'", ex);
            }
        }

        static TimeSpan QueryDelayInterval = TimeSpan.FromMilliseconds(200);

        Action<QueueLengthEntry[], EndpointToQueueMapping> store;
        CancellationTokenSource stop = new CancellationTokenSource();
        Task poller;

        ConcurrentDictionary<EndpointToQueueMapping, string> queues = new ConcurrentDictionary<EndpointToQueueMapping, string>();
        ConcurrentDictionary<string, int> sizes = new ConcurrentDictionary<string, int>();

        string queueNamePrefix;

        Func<IAmazonSQS> clientFactory = () => new AmazonSQSClient();

        static ILog Logger = LogManager.GetLogger<QueueLengthProvider>();
    }
}
