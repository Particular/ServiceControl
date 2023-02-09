namespace ServiceControl.Transport.Tests
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Raw;
    using Transports;
    using Transports.ASB;

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
            customizations = new ASBEndpointTopologyTransportCustomization();
            connectionString = Environment.GetEnvironmentVariable("ServiceControl.TransportTests.ASBS.ConnectionString");

            return Task.CompletedTask;
        }

        public void ApplyTransportConfig(RawEndpointConfiguration c)
        {
#pragma warning disable CS0618
            var transport = c.UseTransport<AzureServiceBusTransport>()
#pragma warning restore CS0618
                .ConnectionString(connectionString);

            transport.UseForwardingTopology();
        }

        string connectionString;
        ASBEndpointTopologyTransportCustomization customizations;
    }
}