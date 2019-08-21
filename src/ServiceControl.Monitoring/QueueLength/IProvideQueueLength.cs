namespace ServiceControl.Monitoring.QueueLength
{
    using System.Threading.Tasks;
    using Infrastructure;
    using Messaging;
    using NServiceBus.Metrics;

    public interface IProvideQueueLength
    {
        void Initialize(string connectionString, QueueLengthStore store);

        void Process(EndpointInstanceId endpointInstanceId, EndpointMetadataReport metadataReport);

        void Process(EndpointInstanceId endpointInstanceId, TaggedLongValueOccurrence metricsReport);

        Task Start();

        Task Stop();
    }
}