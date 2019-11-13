namespace ServiceControl.Transports
{
    using System.Threading.Tasks;

    public class DefaultQueueLengthProvider : IProvideQueueLengthNew
    {
        public void Initialize(string connectionString, QueueLengthStoreDto store)
        {
            queueLengthStore = store;
        }

        public void Process(EndpointInstanceIdDto endpointInstanceId, EndpointMetadataReportDto metadataReport)
        {
            // HINT: Not every queue length provider requires metadata reports
        }

        public void Process(EndpointInstanceIdDto endpointInstanceId, TaggedLongValueOccurrenceDto metricsReport)
        {
            var endpointInputQueue = new EndpointInputQueueDto(endpointInstanceId.EndpointName, metricsReport.TagValue);

            queueLengthStore.Store(metricsReport.Entries, endpointInputQueue);
        }

        public Task Start() => Task.CompletedTask;

        public Task Stop() => Task.CompletedTask;

        QueueLengthStoreDto queueLengthStore;
    }
}