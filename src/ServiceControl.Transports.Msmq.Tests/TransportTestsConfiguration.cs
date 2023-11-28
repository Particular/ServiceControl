namespace ServiceControl.Transport.Tests
{
    using System.Threading.Tasks;
    using ServiceControl.Transports;
    using ServiceControl.Transports.Msmq;

    partial class TransportTestsConfiguration
    {
        public string ConnectionString { get; private set; }

        public ITransportCustomization TransportCustomization { get; private set; }

        public Task Configure()
        {
            TransportCustomization = new MsmqTransportCustomization();
            ConnectionString = null;

            return Task.CompletedTask;
        }

        public Task Cleanup() => Task.CompletedTask;
    }
}