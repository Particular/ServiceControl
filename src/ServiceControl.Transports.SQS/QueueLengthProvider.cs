﻿namespace ServiceControl.Transports.SQS
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Data.Common;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using Amazon.Runtime;
    using Amazon.SQS;
    using NServiceBus.Logging;

    class QueueLengthProvider : AbstractQueueLengthProvider
    {
        public QueueLengthProvider(TransportSettings settings, Action<QueueLengthEntry[], EndpointToQueueMapping> store) : base(settings, store)
        {
            var builder = new DbConnectionStringBuilder { ConnectionString = ConnectionString };
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
        }

        public override void TrackEndpointInputQueue(EndpointToQueueMapping queueToTrack)
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

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var client = clientFactory();
            var cache = new QueueAttributesRequestCache(client);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await FetchQueueSizes(cache, client, stoppingToken);

                    UpdateQueueLengthStore();

                    await Task.Delay(QueryDelayInterval, stoppingToken);
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

        void UpdateQueueLengthStore()
        {
            var nowTicks = DateTime.UtcNow.Ticks;

            foreach (var tableNamePair in queues)
            {
                Store(
                    [
                        new QueueLengthEntry
                        {
                            DateTicks = nowTicks,
                            Value = sizes.GetValueOrDefault(tableNamePair.Value, 0)
                        }
                    ],
                    tableNamePair.Key);
            }
        }

        Task FetchQueueSizes(QueueAttributesRequestCache cache, IAmazonSQS client, CancellationToken cancellationToken) => Task.WhenAll(sizes.Select(kvp => FetchLength(kvp.Key, client, cache, cancellationToken)));

        async Task FetchLength(string queue, IAmazonSQS client, QueueAttributesRequestCache cache, CancellationToken cancellationToken)
        {
            try
            {
                var attReq = await cache.GetQueueAttributesRequest(queue, cancellationToken);
                var response = await client.GetQueueAttributesAsync(attReq, cancellationToken);
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

        static readonly TimeSpan QueryDelayInterval = TimeSpan.FromMilliseconds(200);

        readonly ConcurrentDictionary<EndpointToQueueMapping, string> queues = new ConcurrentDictionary<EndpointToQueueMapping, string>();
        readonly ConcurrentDictionary<string, int> sizes = new ConcurrentDictionary<string, int>();

        readonly string queueNamePrefix;

        readonly Func<IAmazonSQS> clientFactory = () => new AmazonSQSClient();

        static readonly ILog Logger = LogManager.GetLogger<QueueLengthProvider>();
    }
}
