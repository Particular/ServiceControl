namespace ServiceControl.Transports
{
    using System;
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
        public EndpointMetadataReportDto(string localAddress)
        {
            LocalAddress = localAddress;
        }

        public string LocalAddress { get; set; }
    }

    public class EndpointInstanceIdDto
    {
        public string EndpointName { get; set; }
        protected bool Equals(EndpointInstanceIdDto other)
        {
            return string.Equals(EndpointName, other.EndpointName);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;

            return Equals((EndpointInstanceIdDto)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (EndpointName != null ? EndpointName.GetHashCode() : 0);
                return hashCode;
            }
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

        public EndpointInputQueueDto(string endpointName, string inputQueue)
        {
            EndpointName = endpointName;
            InputQueue = inputQueue;
        }

        //TODO: check if that is really used by the providers
        public string InputQueue { get; set; }
        public string EndpointName { get; set; }

        public bool Equals(EndpointInputQueueDto other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(EndpointName, other.EndpointName) && string.Equals(InputQueue, other.InputQueue);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((EndpointInputQueueDto)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((EndpointName != null ? EndpointName.GetHashCode() : 0) * 397) ^ (InputQueue != null ? InputQueue.GetHashCode() : 0);
            }
        }
    }
}