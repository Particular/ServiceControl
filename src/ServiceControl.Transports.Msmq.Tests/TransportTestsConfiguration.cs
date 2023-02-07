namespace ServiceControl.Transport.Tests
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Raw;
    using ServiceControl.Transports;

    partial class TransportTestsConfiguration
    {
        public TransportTestsConfiguration()
        {
        }

        public IProvideQueueLength InitializeQueueLengthProvider(Action<QueueLengthEntry> onQueueLengthReported)
        {
            throw new NotImplementedException();
        }

        public void ApplyTransportConfig(RawEndpointConfiguration c)
        {
            c.UseTransport<MsmqTransport>();
        }
        public Task Cleanup() => throw new NotImplementedException();

        public Task Configure() => throw new NotImplementedException();
    }
}