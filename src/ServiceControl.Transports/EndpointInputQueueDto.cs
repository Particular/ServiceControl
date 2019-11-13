namespace ServiceControl.Transports
{
    public class EndpointInputQueueDto
    {
        public EndpointInputQueueDto(string endpointName, string inputQueue)
        {
            EndpointName = endpointName;
            InputQueue = inputQueue;
        }

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