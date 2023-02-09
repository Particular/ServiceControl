namespace ServiceControl.Transport.Tests
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Raw;
    using Transports;
    using Transports.ASBS;

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
            customizations = new ASBSTransportCustomization();
            connectionString = Environment.GetEnvironmentVariable("ServiceControl.TransportTests.ASBS.ConnectionString");

            return Task.CompletedTask;
        }

        public void ApplyTransportConfig(RawEndpointConfiguration c)
        {
            c.UseTransport<AzureServiceBusTransport>()
                .ConnectionString(connectionString);
        }

        string connectionString;
        ASBSTransportCustomization customizations;
    }
}