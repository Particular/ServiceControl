namespace ServiceControl.Transports.RabbitMQ
{
    using System;
    using System.Collections.Concurrent;
    using System.Data.Common;
    using System.Threading;
    using System.Threading.Tasks;
    using global::RabbitMQ.Client;
    using NServiceBus.Logging;

    public class QueueLengthProvider : IProvideQueueLengthNew
    {
        public void Initialize(string connectionString, QueueLengthStoreDto storeDto)
        {
            queryExecutor = new QueryExecutor(connectionString);

            queueLengthStoreDto = storeDto;
        }

        public void Process(EndpointInstanceIdDto endpointInstanceIdDto, EndpointMetadataReportDto metadataReportDto)
        {
            var endpointInstanceQueue = new EndpointInputQueueDto(endpointInstanceIdDto.EndpointName, metadataReportDto.LocalAddress);
            var queueName = metadataReportDto.LocalAddress;

            endpointQueues.AddOrUpdate(endpointInstanceQueue, _ => queueName, (_, currentValue) =>
            {
                if (currentValue != queueName)
                {
                    sizes.TryRemove(currentValue, out var _);
                }

                return queueName;
            });
        }

        public void Process(EndpointInstanceIdDto endpointInstanceIdDto, TaggedLongValueOccurrenceDto metricsReport)
        {
            //RabbitMQ does not support endpoint level queue length reports
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
                    queueLengthStoreDto.Store(
                        new[]
                        {
                            new EntryDto
                            {
                                DateTicks = nowTicks,
                                Value = size
                            }
                        },
                        endpointQueuePair.Key);
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

        QueueLengthStoreDto queueLengthStoreDto;
        QueryExecutor queryExecutor;
        static TimeSpan QueryDelayInterval = TimeSpan.FromMilliseconds(200);

        ConcurrentDictionary<EndpointInputQueueDto, string> endpointQueues = new ConcurrentDictionary<EndpointInputQueueDto, string>();
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

                var dbConnectionStringBuilder = new DbConnectionStringBuilder {ConnectionString = connectionString};

                connectionFactory = new ConnectionFactory(connectionConfiguration,
                    null, //providing certificates is not supported yet
                    dbConnectionStringBuilder.GetBooleanValue("DisableRemoteCertificateValidation"),
                    dbConnectionStringBuilder.GetBooleanValue("UseExternalAuthMechanism"));
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
