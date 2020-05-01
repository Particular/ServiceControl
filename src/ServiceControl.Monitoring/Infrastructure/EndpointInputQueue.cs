namespace ServiceControl.Monitoring.Infrastructure
{
    using System;

    public class EndpointInputQueue : IEquatable<EndpointInputQueue>
    {
        public EndpointInputQueue(string endpointName, string inputQueue)
        {
            EndpointName = endpointName;
            InputQueue = inputQueue;
        }

        public string EndpointName { get; }
        public string InputQueue { get; }

        public bool Equals(EndpointInputQueue other)
        {
            if (ReferenceEquals(null, other))
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
            if (ReferenceEquals(null, obj))
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

            return Equals((EndpointInputQueue)obj);
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