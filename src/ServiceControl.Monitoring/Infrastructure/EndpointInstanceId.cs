namespace ServiceControl.Monitoring.Infrastructure
{
    using System.Collections.Generic;
    using NServiceBus;

    public class EndpointInstanceId
    {
        public EndpointInstanceId(string endpointName, string instanceId)
            : this(endpointName, instanceId, instanceId)
        {
        }

        public EndpointInstanceId(string endpointName, string instanceId, string instanceName)
        {
            EndpointName = endpointName;
            InstanceId = instanceId;
            InstanceName = instanceName;
        }

        public string EndpointName { get; }
        public string InstanceId { get; }
        public string InstanceName { get; }

        public static EndpointInstanceId From(IReadOnlyDictionary<string, string> headers)
        {
            var endpointName = headers[Headers.OriginatingEndpoint];

            if (headers.TryGetValue(MetricHeaders.MetricInstanceId, out var instanceId))
            {
                return new EndpointInstanceId(endpointName, instanceId);
            }

            var hostId = headers[Headers.OriginatingHostId];
            var hostDisplayName = headers[Headers.HostDisplayName];

            return new EndpointInstanceId(endpointName, hostId, hostDisplayName);
        }

        protected bool Equals(EndpointInstanceId other)
        {
            return string.Equals(EndpointName, other.EndpointName) &&
                   string.Equals(InstanceId, other.InstanceId);
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

            return Equals((EndpointInstanceId)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (EndpointName != null ? EndpointName.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (InstanceId != null ? InstanceId.GetHashCode() : 0);
                return hashCode;
            }
        }
    }
}