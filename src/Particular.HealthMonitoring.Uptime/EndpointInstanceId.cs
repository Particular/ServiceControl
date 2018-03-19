namespace ServiceControl.Monitoring
{
    using System;
    using ServiceControl.Infrastructure;

    public class EndpointInstanceId : IEquatable<EndpointInstanceId>
    {
        public readonly string LogicalName;
        public readonly string HostName;
        public readonly Guid HostGuid;
        public Guid UniqueId { get; }

        public EndpointInstanceId(string logicalName, string hostName, Guid hostGuid)
        {
            LogicalName = logicalName;
            HostName = hostName;
            HostGuid = hostGuid;
            UniqueId = DeterministicGuid.MakeId(LogicalName, HostGuid.ToString());
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as EndpointInstanceId);
        }

        public bool Equals(EndpointInstanceId other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(LogicalName, other.LogicalName) && string.Equals(HostName, other.HostName) && HostGuid.Equals(other.HostGuid);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (LogicalName != null ? LogicalName.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (HostName != null ? HostName.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ HostGuid.GetHashCode();
                return hashCode;
            }
        }

    }
}