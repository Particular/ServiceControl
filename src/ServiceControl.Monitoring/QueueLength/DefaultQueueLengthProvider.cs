namespace ServiceControl.Monitoring.QueueLength
{
    using System.Threading.Tasks;
    using Transports;

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

        public Task Start() => TaskEx.Completed;

        public Task Stop() => TaskEx.Completed;
        QueueLengthStoreDto queueLengthStore;
    }
}