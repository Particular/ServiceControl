namespace ServiceControl.Transports.AmazonSQS
{
    using System;
    using System.Collections.Concurrent;
    using System.Data.Common;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.Runtime;
    using Amazon.SQS;
    using Monitoring;
    using Monitoring.Infrastructure;
    using Monitoring.Messaging;
    using Monitoring.QueueLength;
    using NServiceBus.Logging;
    using NServiceBus.Metrics;

    public class QueueLengthProvider
    {
        ConcurrentDictionary<EndpointInputQueue, string> queues = new ConcurrentDictionary<EndpointInputQueue, string>();
        ConcurrentDictionary<string, int> sizes = new ConcurrentDictionary<string, int>();

        QueueLengthStore store;

        string queueNamePrefix;
        CancellationTokenSource stop = new CancellationTokenSource();
        Task poller;
        Func<IAmazonSQS> clientFactory = () => new AmazonSQSClient();


        public void Initialize(string connectionString, QueueLengthStore store)
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
            this.store = store;
        }

        public void Process(EndpointInstanceId endpointInstanceId, EndpointMetadataReport metadataReport)
        {
            var endpointInputQueue = new EndpointInputQueue(endpointInstanceId.EndpointName, metadataReport.LocalAddress);
            var queue = QueueNameHelper.GetSqsQueueName(metadataReport.LocalAddress, queueNamePrefix);

            queues.AddOrUpdate(endpointInputQueue, _ => queue, (_, currentQueue) =>
            {
                if (currentQueue != queue)
                {
                    sizes.TryRemove(currentQueue, out var _);
                }

                return queue;
            });

            sizes.TryAdd(queue, 0);
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

            foreach (var tableNamePair in queues)
            {
                store.Store(
                    new[]{ new RawMessage.Entry
                    {
                        DateTicks = nowTicks,
                        Value = sizes.TryGetValue(tableNamePair.Value, out var size) ? size : 0
                    }},
                    tableNamePair.Key);
            }
        }

        Task FetchQueueSizes(QueueAttributesRequestCache cache, IAmazonSQS client, CancellationToken token) => Task.WhenAll(sizes.Select(kvp => FetchLength(kvp.Key, client, cache, token)));


        async Task FetchLength(string queue, IAmazonSQS client, QueueAttributesRequestCache cache, CancellationToken token)
        {
            try
            {
                var attReq = await cache.GetQueueAttributesRequest(queue, token).ConfigureAwait(false);
                var response = await client.GetQueueAttributesAsync(attReq, token).ConfigureAwait(false);
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
        static ILog Logger = LogManager.GetLogger<QueueLengthProvider>();
    }
}