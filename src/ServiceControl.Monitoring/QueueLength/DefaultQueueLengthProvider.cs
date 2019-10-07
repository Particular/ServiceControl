namespace ServiceControl.Monitoring.QueueLength
{
    using System.Threading.Tasks;
    using Infrastructure;
    using Messaging;
    using NServiceBus.Metrics;

    public class DefaultQueueLengthProvider : IProvideQueueLength
    {
        public void Initialize(string connectionString, QueueLengthStore store)
        {
            queueLengthStore = store;
        }

        public void Process(EndpointInstanceId endpointInstanceId, EndpointMetadataReport metadataReport)
        {
            // HINT: Not every queue length provider requires metadata reports
        }

        public void Process(EndpointInstanceId endpointInstanceId, TaggedLongValueOccurrence metricsReport)
        {
            var endpointInputQueue = new EndpointInputQueue(endpointInstanceId.EndpointName, metricsReport.TagValue);

            queueLengthStore.Store(metricsReport.Entries, endpointInputQueue);
        }

        public Task Start() => TaskEx.Completed;

        public Task Stop() => TaskEx.Completed;
        QueueLengthStore queueLengthStore;
    }
}