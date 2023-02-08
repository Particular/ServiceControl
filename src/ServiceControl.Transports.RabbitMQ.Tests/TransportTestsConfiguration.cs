namespace ServiceControl.Transport.Tests
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
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
            connectionString = Environment.GetEnvironmentVariable("ServiceControl.AcceptanceTests.ConnectionString");

            return Task.CompletedTask;
        }

        public void ApplyTransportConfig(RawEndpointConfiguration c)
        {
            c.UseTransport<RabbitMQTransport>()
                .UseConventionalRoutingTopology(QueueType.Quorum)
                .ConnectionString(connectionString);
        }

        string connectionString;
        RabbitMQQuorumConventionalRoutingTransportCustomization customizations;
    }
}