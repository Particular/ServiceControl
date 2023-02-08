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

            queueLengthProvider.Initialize(ConnectionString(), store);

            return queueLengthProvider;
        }

        public Task Cleanup() => Task.CompletedTask;

        public Task Configure()
        {
            customizations = new ASBSTransportCustomization();

            return Task.CompletedTask;
        }

        public void ApplyTransportConfig(RawEndpointConfiguration c)
        {
            c.UseTransport<AzureServiceBusTransport>()
                .ConnectionString(ConnectionString());
        }

        string ConnectionString()
        {
            var connectionString =
                Environment.GetEnvironmentVariable("ServiceControl.TransportTests.ASBS.ConnectionString");
            //TODO: make localDb work in the CI or think about a convenient way for the developer workflow, 
            //perhaps something similar to the connection.txt file used by acceptance tests
            // or the developer can set the environment variable via launchSettings.json
#if DEBUG
            connectionString = "";
#endif      
            return connectionString;
        }

        ASBSTransportCustomization customizations;
    }
}