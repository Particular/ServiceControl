namespace ServiceControl.Monitoring.PerformanceTests
{
    using ServiceControl.Transports;
    using System.Threading.Tasks;

    class FakeQueueLengthProvider : IProvideQueueLength
    {
        public void Initialize(string connectionString, QueueLengthStoreDto store)
        {
            queueLengthStore = store;
        }

        public void TrackEndpointInputQueue(string endpointName, string queueAddress)
        {
        }

        public Task Start() => Task.CompletedTask;

        public Task Stop() => Task.CompletedTask;

        QueueLengthStoreDto queueLengthStore;
    }
}