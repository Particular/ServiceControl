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
            connectionString = Environment.GetEnvironmentVariable(ConnectionStringKey);

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new Exception($"Environment variable {ConnectionStringKey} is required for ASB Forwarding topology transport tests to run");
            }

            return Task.CompletedTask;
        }

        public void ApplyTransportConfig(RawEndpointConfiguration c)
        {
#pragma warning disable CS0618
            c.UseTransport<AzureServiceBusTransport>()
#pragma warning restore CS0618
                .ConnectionString(connectionString)
                .UseForwardingTopology();
        }

        string connectionString;
        ASBEndpointTopologyTransportCustomization customizations;

        static string ConnectionStringKey = "ServiceControl.TransportTests.ASBS.ConnectionString";
    }
}