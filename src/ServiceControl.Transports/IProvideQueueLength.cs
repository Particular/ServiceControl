namespace ServiceControl.Transports
{
    using System.Threading.Tasks;

    public interface IProvideQueueLength
    {
        void Initialize(string connectionString, QueueLengthStore store);

        void Process(EndpointInstanceId endpointInstanceId, EndpointMetadataReport metadataReport);

        void Process(EndpointInstanceId endpointInstanceId, TaggedLongValueOccurrence metricsReport);

        Task Start();

        Task Stop();
    }
}