namespace ServiceControl.Transports
{
    using System.Threading.Tasks;

    public interface IProvideQueueLength
    {
        void Initialize(string connectionString, QueueLengthStoreDto storeDto);

        void Process(EndpointInstanceIdDto endpointInstanceIdDto, string queueAddress);

        void Process(EndpointInstanceIdDto endpointInstanceIdDto, TaggedLongValueOccurrenceDto metricsReport);

        Task Start();

        Task Stop();
    }
}