namespace ServiceControl.Transports
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IProvideQueueLengthNew
    {
        void Initialize(string connectionString, QueueLengthStoreDto storeDto);

        void Process(EndpointInstanceIdDto endpointInstanceIdDto, EndpointMetadataReportDto metadataReportDto);

        void Process(EndpointInstanceIdDto endpointInstanceIdDto, TaggedLongValueOccurrenceDto metricsReport);

        Task Start();

        Task Stop();
    }

    public class TaggedLongValueOccurrenceDto
    {
        public TaggedLongValueOccurrenceDto(EntryDto[] messageEntries, string messageTagValue)
        {
            throw new NotImplementedException();
        }

        public EntryDto[] Entries { get; set; }
        public string TagValue { get; set; }
    }

    public class EndpointMetadataReportDto
    {
        public EndpointMetadataReportDto(string messageLocalAddress)
        {
            throw new NotImplementedException();
        }

        public string LocalAddress { get; set; }
    }

    public class EndpointInstanceIdDto
    {
        public string EndpointName { get; set; }

        public static EndpointInstanceIdDto From(IReadOnlyDictionary<string, string> contextMessageHeaders)
        {
            throw new NotImplementedException();
        }
    }

    public class QueueLengthStoreDto
    {
        readonly Action<EntryDto[], EndpointInputQueueDto> store;

        public QueueLengthStoreDto(Action<EntryDto[], EndpointInputQueueDto> store)
        {
            this.store = store;
        }
        public void Store(EntryDto[] entry, EndpointInputQueueDto instance)
        {
            store(entry, instance);
        }
    }

    public class EntryDto
    {
        public long DateTicks { get; set; }
        public long Value { get; set; }
    }

    public class EndpointInputQueueDto
    {

        public EndpointInputQueueDto(string endpointName, string localAddress)
        {
            EndpointName = endpointName;
            LocalAddress = localAddress;
        }

        //TODO: check if that is really used by the providers
        public string LocalAddress { get; set; }

        public string InputQueue { get; set; }
        public string EndpointName { get; set; }
    }
}