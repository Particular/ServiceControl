namespace ServiceControl
{
    using System.Collections.Generic;
    using ServiceControl.Contracts.Operations;

    public class EndpointInstanceId
    {
        public string EndpointName { get; }
        public string InstanceId { get; }
        public string InstanceName { get; }


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

        public static EndpointInstanceId From(IReadOnlyDictionary<string, string> headers)
        {
            var details = EndpointDetailsParser.ReceivingEndpoint(headers);

            if (details == null)
            {
                return null;
            }

            string instanceId;
            headers.TryGetValue("NServiceBus.Metric.InstanceId", out instanceId);

            return new EndpointInstanceId(details.Name, instanceId ?? details.HostId.ToString("N"), details.Host);
        }

        protected bool Equals(EndpointInstanceId other)
        {
            return string.Equals(EndpointName, other.EndpointName) &&
                   string.Equals(InstanceId, other.InstanceId) &&
                   string.Equals(InstanceName, other.InstanceName);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;

            return Equals((EndpointInstanceId)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (EndpointName != null ? EndpointName.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (InstanceId != null ? InstanceId.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (InstanceName != null ? InstanceName.GetHashCode() : 0);
                return hashCode;
            }
        }
    }
}