namespace ServiceControl.Transport.Tests
{
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.Transports;
    using ServiceControl.Transports.Msmq;

    partial class TransportTestsConfiguration
    {
        public string ConnectionString { get; private set; }

        public ITransportCustomization TransportCustomization { get; private set; }

        public Task Configure(CancellationToken cancellationToken = default)
        {
            TransportCustomization = new MsmqTransportCustomization();
            ConnectionString = null;

            return Task.CompletedTask;
        }

        public Task Cleanup(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}