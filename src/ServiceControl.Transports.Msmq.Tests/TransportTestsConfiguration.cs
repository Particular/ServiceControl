namespace ServiceControl.Transport.Tests
{
    using System;
    using System.Threading.Tasks;
    using ServiceControl.Transports;

    partial class TransportTestsConfiguration
    {
        public TransportTestsConfiguration()
        {
        }

        public IProvideQueueLength InitializeQueueLengthProvider(string queueName, Action<QueueLengthEntry> onQueueLengthReported)
        {
            throw new NotImplementedException();
        }

        public Task Cleanup() => throw new NotImplementedException();

        public Task Configure() => throw new NotImplementedException();
    }
}