namespace ServiceControl.Transport.Tests
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Raw;
    using ServiceControl.Transports.RabbitMQ;
    using Transports;

    partial class TransportTestsConfiguration
    {
        public IProvideQueueLength InitializeQueueLengthProvider(Action<QueueLengthEntry[], EndpointToQueueMapping> store)
        {
            var queueLengthProvider = customizations.CreateQueueLengthProvider();

            queueLengthProvider.Initialize(connectionString, store);

            return queueLengthProvider;
        }

        public Task Cleanup() => Task.CompletedTask;

        public Task Configure()
        {
            customizations = new RabbitMQQuorumConventionalRoutingTransportCustomization();
            connectionString = Environment.GetEnvironmentVariable(ConnectionStringKey);

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new Exception($"Environment variable {ConnectionStringKey} is required for RabbitMQ direct routing with classic queues transport tests to run");
            }

            return Task.CompletedTask;
        }

        public void ApplyTransportConfig(RawEndpointConfiguration c)
        {
            c.UseTransport<RabbitMQTransport>()
                .UseDirectRoutingTopology(QueueType.Classic)
                .ConnectionString(connectionString);
        }

        string connectionString;
        RabbitMQQuorumConventionalRoutingTransportCustomization customizations;

        static string ConnectionStringKey = "ServiceControl.TransportTests.RabbitMQ.ConnectionString";
    }
}