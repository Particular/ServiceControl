namespace ServiceControl.Transports
{
    using System.Threading.Tasks;

    public interface IProvideQueueLengthNew
    {
        void Initialize(string connectionString, QueueLengthStoreDto storeDto);

        void Process(EndpointInstanceIdDto endpointInstanceIdDto, EndpointMetadataReportDto metadataReportDto);

        void Process(EndpointInstanceIdDto endpointInstanceIdDto, TaggedLongValueOccurrenceDto metricsReport);

        Task Start();

        Task Stop();
    }
}