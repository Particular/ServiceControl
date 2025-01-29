namespace ServiceControl.Transports.RabbitMQ
{
    using System;
    using System.Collections.Concurrent;
    using System.Data.Common;
    using System.Threading;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;
    using NServiceBus.Logging;

    class QueueLengthProvider : AbstractQueueLengthProvider
    {
        public QueueLengthProvider(TransportSettings settings, Action<QueueLengthEntry[], EndpointToQueueMapping> store) : base(settings, store)
        {
            queryExecutor = new QueryExecutor(ConnectionString);
            queryExecutor.Initialize();
        }

        public override void TrackEndpointInputQueue(EndpointToQueueMapping queueToTrack) =>
            endpointQueues.AddOrUpdate(queueToTrack.EndpointName, _ => queueToTrack.InputQueue, (_, currentValue) =>
            {
                if (currentValue != queueToTrack.InputQueue)
                {
                    sizes.TryRemove(currentValue, out var _);
                }

                return queueToTrack.InputQueue;
            });

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await FetchQueueLengths(stoppingToken);

                    UpdateQueueLengths();

                    await Task.Delay(QueryDelayInterval, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // no-op
                }
                catch (Exception e)
                {
                    Logger.Error("Queue length query loop failure.", e);
                }
            }
        }

        void UpdateQueueLengths()
        {
            var nowTicks = DateTime.UtcNow.Ticks;

            foreach (var endpointQueuePair in endpointQueues)
            {
                if (sizes.TryGetValue(endpointQueuePair.Value, out var size))
                {
                    Store(
                        [
                            new QueueLengthEntry
                            {
                                DateTicks = nowTicks,
                                Value = size
                            }
                        ],
                        new EndpointToQueueMapping(endpointQueuePair.Key, endpointQueuePair.Value));
                }
            }
        }

        async Task FetchQueueLengths(CancellationToken cancellationToken)
        {
            foreach (var endpointQueuePair in endpointQueues)
            {
                await queryExecutor.Execute(async m =>
                {
                    var queueName = endpointQueuePair.Value;

                    try
                    {
                        var size = (int)await m.MessageCountAsync(queueName, cancellationToken).ConfigureAwait(false);

                        sizes.AddOrUpdate(queueName, _ => size, (_, __) => size);
                    }
                    catch (Exception e)
                    {
                        Logger.Warn($"Error querying queue length for {queueName}", e);
                    }
                }, cancellationToken);
            }
        }

        readonly QueryExecutor queryExecutor;
        static readonly TimeSpan QueryDelayInterval = TimeSpan.FromMilliseconds(200);

        readonly ConcurrentDictionary<string, string> endpointQueues = new ConcurrentDictionary<string, string>();
        readonly ConcurrentDictionary<string, int> sizes = new ConcurrentDictionary<string, int>();

        static readonly ILog Logger = LogManager.GetLogger<QueueLengthProvider>();

        class QueryExecutor(string connectionString) : IDisposable
        {

            public void Initialize()
            {
                var connectionConfiguration =
                    ConnectionConfiguration.Create(connectionString, "ServiceControl.Monitoring");

                var dbConnectionStringBuilder = new DbConnectionStringBuilder { ConnectionString = connectionString };

                connectionFactory = new ConnectionFactory("ServiceControl.Monitoring",
                    connectionConfiguration,
                    null, //providing certificates is not supported yet
                    dbConnectionStringBuilder.GetBooleanValue("DisableRemoteCertificateValidation"),
                    dbConnectionStringBuilder.GetBooleanValue("UseExternalAuthMechanism"),
                    null, // value would come from config API in actual transport
                    null); // value would come from config API in actual transport
            }

            public async Task Execute(Action<IChannel> action, CancellationToken cancellationToken = default)
            {
                try
                {
                    connection ??= connectionFactory.CreateConnection("queue length monitor");

                    //Connection implements reconnection logic
                    while (!connection.IsOpen)
                    {
                        await Task.Delay(ReconnectionDelay, cancellationToken);
                    }

                    if (channel == null || channel.IsClosed)
                    {
                        channel?.Dispose();

                        channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                    }

                    action(channel);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // no-op
                }
                catch (Exception e)
                {
                    Logger.Warn("Error querying queue length.", e);
                }
            }

            public void Dispose() => connection?.Dispose();

            IConnection connection;
            IChannel channel;
            ConnectionFactory connectionFactory;

            static readonly TimeSpan ReconnectionDelay = TimeSpan.FromSeconds(5);
        }
    }
}
