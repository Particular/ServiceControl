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
        public string ConnectionString { get; private set; }

        public TransportCustomization TransportCustomization { get; private set; }

        public Task Configure()
        {
            TransportCustomization = new RabbitMQQuorumConventionalRoutingTransportCustomization();
            ConnectionString = Environment.GetEnvironmentVariable(ConnectionStringKey);

            if (string.IsNullOrEmpty(ConnectionString))
            {
                throw new Exception($"Environment variable {ConnectionStringKey} is required for RabbitMQ direct routing with quorum queues transport tests to run");
            }

            return Task.CompletedTask;
        }

        public Task Cleanup() => Task.CompletedTask;

        public void ApplyTransportConfig(RawEndpointConfiguration c)
        {
            c.UseTransport<RabbitMQTransport>()
                .UseDirectRoutingTopology(QueueType.Quorum)
                .ConnectionString(ConnectionString);
        }

        static string ConnectionStringKey = "ServiceControl.TransportTests.RabbitMQ.ConnectionString";
    }
}