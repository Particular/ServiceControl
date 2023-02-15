namespace ServiceControl.Transport.Tests
{
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Raw;
    using ServiceControl.Transports;
    using ServiceControl.Transports.Msmq;

    partial class TransportTestsConfiguration
    {
        public string ConnectionString { get; private set; }

        public TransportCustomization TransportCustomization { get; private set; }

        public Task Configure()
        {
            TransportCustomization = new MsmqTransportCustomization();
            ConnectionString = null;

            return Task.CompletedTask;
        }

        public void ApplyTransportConfig(RawEndpointConfiguration c)
        {
            c.UseTransport<MsmqTransport>();
        }

        public Task Cleanup() => Task.CompletedTask;
    }
}