namespace ServiceControl.Transports.ASBS
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.ServiceBus.Management;
    using NServiceBus.Logging;

    class QueueLengthProvider : IProvideQueueLength
    {
        public void Initialize(string connectionString, QueueLengthStoreDto store)
        {
            var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };

            if (builder.TryGetValue(QueueLengthQueryIntervalPartName, out var value))
            {
                if (int.TryParse(value.ToString(), out var queryDelayInterval))
                {
                    QueryDelayInterval = TimeSpan.FromMilliseconds(queryDelayInterval);
                }
                else
                {
                    Logger.Warn($"Can't parse {value} as a valid query delay interval.");
                }
            }

            this.queueLengthStore = store;
            this.managementClient = new ManagementClient(connectionString);
        }

        public void TrackEndpointInputQueue(string endpointName, string queueAddress)
        {
            endpointQueueMappings.AddOrUpdate(
                queueAddress,
                id => endpointName,
                (id, old) => endpointName
            );
        }

        public void Process(string endpointName, TaggedLongValueOccurrenceDto metricsReport)
        {
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
                        Logger.Debug("Waiting for next interval");
                        await Task.Delay(QueryDelayInterval, token).ConfigureAwait(false);

                        Logger.DebugFormat("Querying management client.");

                        var queues = await GetQueueList(token).ConfigureAwait(false);

                        Logger.DebugFormat("Retrieved details of {0} queues", queues.Count);

                        var queuesLookup = new ConcurrentDictionary<string, QueueDescription>(queues, StringComparer.InvariantCultureIgnoreCase);

                        await UpdateAllQueueLengths(queuesLookup, token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // no-op
                    }
                    catch (Exception e)
                    {
                        Logger.Error("Error querying Azure Service Bus queue sizes.", e);
                    }
                }
            });

            return Task.CompletedTask;
        }

        async Task<Dictionary<string, QueueDescription>> GetQueueList(CancellationToken token)
        {
            var pageSize = 100; //This is the maximal page size for GetQueueAsync
            var pageNo = 0;

            var queues = new List<QueueDescription>();

            while (true)
            {
                var page = await managementClient.GetQueuesAsync(count: pageSize, skip: pageNo * pageSize, cancellationToken: token).ConfigureAwait(false);

                queues.AddRange(page);

                if (page.Count < pageSize)
                {
                    break;
                }

                pageNo++;
            }

            return queues.ToDictionary(q => q.Path, q => q);
        }

        Task UpdateAllQueueLengths(ConcurrentDictionary<string, QueueDescription> queues, CancellationToken token) => Task.WhenAll(endpointQueueMappings.Select(eq => UpdateQueueLength(eq, queues, token)));

        async Task UpdateQueueLength(KeyValuePair<string, string> monitoredEndpoint, ConcurrentDictionary<string, QueueDescription> queues, CancellationToken token)
        {
            var endpointName = monitoredEndpoint.Value;
            var queueName = monitoredEndpoint.Key;

            if (!queues.TryGetValue(queueName, out _))
            {
                return;
            }

            var entries = new[]
            {
                new EntryDto
                {
                    DateTicks =  DateTime.UtcNow.Ticks,
                    Value = (await managementClient.GetQueueRuntimeInfoAsync(queueName, token).ConfigureAwait(false)).MessageCountDetails.ActiveMessageCount
                }
            };

            queueLengthStore.Store(entries, new EndpointInputQueueDto(endpointName, queueName));
        }

        public async Task Stop()
        {
            stop.Cancel();
            await poller.ConfigureAwait(false);
            await managementClient.CloseAsync().ConfigureAwait(false);
        }

        ConcurrentDictionary<string, string> endpointQueueMappings = new ConcurrentDictionary<string, string>();
        QueueLengthStoreDto queueLengthStore;
        ManagementClient managementClient;
        CancellationTokenSource stop = new CancellationTokenSource();
        Task poller;

        static TimeSpan QueryDelayInterval = TimeSpan.FromMilliseconds(500);
        static string QueueLengthQueryIntervalPartName = "QueueLengthQueryDelayInterval";
        static ILog Logger = LogManager.GetLogger<QueueLengthProvider>();
    }
}