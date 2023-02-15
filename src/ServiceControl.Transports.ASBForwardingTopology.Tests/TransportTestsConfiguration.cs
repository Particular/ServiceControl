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
        public string ConnectionString { get; private set; }

        public TransportCustomization TransportCustomization { get; private set; }

        public Task Configure()
        {
            TransportCustomization = new ASBEndpointTopologyTransportCustomization();
            ConnectionString = Environment.GetEnvironmentVariable(ConnectionStringKey);

            if (string.IsNullOrEmpty(ConnectionString))
            {
                throw new Exception($"Environment variable {ConnectionStringKey} is required for ASB Forwarding topology transport tests to run");
            }

            return Task.CompletedTask;
        }

        public Task Cleanup() => Task.CompletedTask;


        public void ApplyTransportConfig(RawEndpointConfiguration c)
        {
#pragma warning disable CS0618
            c.UseTransport<AzureServiceBusTransport>()
#pragma warning restore CS0618
                .ConnectionString(ConnectionString)
                .UseForwardingTopology();
        }

        static string ConnectionStringKey = "ServiceControl.TransportTests.ASBS.ConnectionString";
    }
}