namespace ServiceControl.Transports
{
    public class EndpointToQueueMapping
    {
        public EndpointToQueueMapping(string endpointName, string inputQueue)
        {
            EndpointName = endpointName;
            InputQueue = inputQueue;
        }

        public string InputQueue { get; set; }

        public string EndpointName { get; set; }

        public bool Equals(EndpointToQueueMapping other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return string.Equals(EndpointName, other.EndpointName) && string.Equals(InputQueue, other.InputQueue);
        }

        public override bool Equals(object obj)
        {
            if (obj is null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != GetType())
            {
                return false;
            }

            return Equals((EndpointToQueueMapping)obj);
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