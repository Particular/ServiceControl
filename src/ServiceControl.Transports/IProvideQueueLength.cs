namespace ServiceControl.Transports
{
    using System.Threading.Tasks;

    public interface IProvideQueueLength
    {
        void Initialize(string connectionString, QueueLengthStoreDto storeDto);

        void TrackEndpointInputQueue(string endpointName, string queueAddress);

        void Process(string endpointNameDto, TaggedLongValueOccurrenceDto metricsReport);

        Task Start();

        Task Stop();
    }
}