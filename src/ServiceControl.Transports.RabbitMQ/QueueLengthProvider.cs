namespace ServiceControl.Transports.RabbitMQ
{
    using System;
    using System.Collections.Concurrent;
    using System.Data.Common;
    using System.Threading;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;
    using NServiceBus.Logging;

    class QueueLengthProvider : IProvideQueueLength
    {
        public void Initialize(string connectionString, Action<QueueLengthEntry[], EndpointToQueueMapping> store)
        {
            queryExecutor = new QueryExecutor(connectionString);

            this.store = store;
        }

        public void TrackEndpointInputQueue(EndpointToQueueMapping queueToTrack)
        {
            endpointQueues.AddOrUpdate(queueToTrack.EndpointName, _ => queueToTrack.InputQueue, (_, currentValue) =>
            {
                if (currentValue != queueToTrack.InputQueue)
                {
                    sizes.TryRemove(currentValue, out var _);
                }

                return queueToTrack.InputQueue;
            });
        }

        public Task Start()
        {
            queryExecutor.Initialize();

            poller = Task.Run(async () =>
            {
                var token = stoppedTokenSource.Token;
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await FetchQueueLengths(token).ConfigureAwait(false);

                        UpdateQueueLengths();

                        await Task.Delay(QueryDelayInterval, token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // no-op
                    }
                    catch (Exception e)
                    {
                        Logger.Error("Queue length query loop failure.", e);
                    }
                }
            });

            return Task.CompletedTask;
        }

        void UpdateQueueLengths()
        {
            var nowTicks = DateTime.UtcNow.Ticks;

            foreach (var endpointQueuePair in endpointQueues)
            {
                if (sizes.TryGetValue(endpointQueuePair.Value, out var size))
                {
                    store(
                        new[]
                        {
                            new QueueLengthEntry
                            {
                                DateTicks = nowTicks,
                                Value = size
                            }
                        },
                        new EndpointToQueueMapping(endpointQueuePair.Key, endpointQueuePair.Value));
                }
            }
        }

        async Task FetchQueueLengths(CancellationToken token)
        {
            foreach (var endpointQueuePair in endpointQueues)
            {
                await queryExecutor.Execute(m =>
                {
                    var queueName = endpointQueuePair.Value;

                    try
                    {
                        var size = (int)m.MessageCount(queueName);

                        sizes.AddOrUpdate(queueName, _ => size, (_, __) => size);
                    }
                    catch (Exception e)
                    {
                        Logger.Warn($"Error querying queue length for {queueName}", e);
                    }
                }, token).ConfigureAwait(false);
            }
        }

        public async Task Stop()
        {
            stoppedTokenSource.Cancel();

            await poller.ConfigureAwait(false);

            queryExecutor.Dispose();
        }

        Action<QueueLengthEntry[], EndpointToQueueMapping> store;
        QueryExecutor queryExecutor;
        static TimeSpan QueryDelayInterval = TimeSpan.FromMilliseconds(200);

        ConcurrentDictionary<string, string> endpointQueues = new ConcurrentDictionary<string, string>();
        ConcurrentDictionary<string, int> sizes = new ConcurrentDictionary<string, int>();

        CancellationTokenSource stoppedTokenSource = new CancellationTokenSource();
        Task poller;

        static ILog Logger = LogManager.GetLogger<QueueLengthProvider>();

        class QueryExecutor : IDisposable
        {
            string connectionString;
            IConnection connection;
            IModel model;
            ConnectionFactory connectionFactory;

            public QueryExecutor(string connectionString)
            {
                this.connectionString = connectionString;
            }

            public void Initialize()
            {
                var connectionConfiguration = ConnectionConfiguration.Create(connectionString, "ServiceControl.Monitoring");

                var dbConnectionStringBuilder = new DbConnectionStringBuilder { ConnectionString = connectionString };

                connectionFactory = new ConnectionFactory("ServiceControl.Monitoring",
                    connectionConfiguration,
                    null, //providing certificates is not supported yet
                    dbConnectionStringBuilder.GetBooleanValue("DisableRemoteCertificateValidation"),
                    dbConnectionStringBuilder.GetBooleanValue("UseExternalAuthMechanism"),
                    null, // value would come from config API in actual transport
                    null); // value would come from config API in actual transport
            }

            public async Task Execute(Action<IModel> action, CancellationToken token)
            {
                try
                {
                    if (connection == null)
                    {
                        connection = connectionFactory.CreateConnection("queue length monitor");
                    }

                    //Connection implements reconnection logic
                    while (connection.IsOpen == false)
                    {
                        await Task.Delay(ReconnectionDelay, token).ConfigureAwait(false);
                    }

                    if (model == null || model.IsClosed)
                    {
                        model?.Dispose();

                        model = connection.CreateModel();
                    }

                    action(model);
                }
                catch (OperationCanceledException)
                {
                    // no-op
                }
                catch (Exception e)
                {
                    Logger.Warn("Error querying queue length.", e);
                }
            }

            TimeSpan ReconnectionDelay = TimeSpan.FromSeconds(5);

            public void Dispose()
            {
                connection?.Dispose();
            }
        }
    }
}
