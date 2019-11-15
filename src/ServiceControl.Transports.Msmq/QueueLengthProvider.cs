namespace ServiceControl.Transports.Msmq
{
    using System.Threading.Tasks;

    class QueueLengthProvider : IProvideQueueLength
    {
        public void Initialize(string connectionString, QueueLengthStoreDto store)
        {
            queueLengthStore = store;
        }

        public void TrackEndpointInputQueue(string endpointName, string queueAddress)
        {
            // HINT: Not every queue length provider requires metadata reports
        }

        public void Process(string endpointName, TaggedLongValueOccurrenceDto metricsReport)
        {
            var endpointInputQueue = new EndpointInputQueueDto(endpointName, metricsReport.TagValue);

            queueLengthStore.Store(metricsReport.Entries, endpointInputQueue);
        }

        public Task Start() => Task.CompletedTask;

        public Task Stop() => Task.CompletedTask;

        QueueLengthStoreDto queueLengthStore;
    }
}