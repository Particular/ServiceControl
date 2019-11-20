namespace ServiceControl.Monitoring.AcceptanceTests.Tests
{
    using System;
    using System.Threading.Tasks;
    using Transports;

    class FakeQueueLengthProvider : IProvideQueueLength
    {
        public void Initialize(string connectionString, Action<QueueLengthEntry[], EndpointToQueueMapping> store)
        {
        }

        public void TrackEndpointInputQueue(EndpointToQueueMapping queueToTrack)
        {
        }

        public Task Start() => Task.CompletedTask;

        public Task Stop() => Task.CompletedTask;
    }
}