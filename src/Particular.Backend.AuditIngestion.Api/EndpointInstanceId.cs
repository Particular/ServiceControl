namespace Particular.Backend.AuditIngestion.Api
{
    using System;

    /// <summary>
    /// Identifies a physical endpoint instance.
    /// </summary>
    public sealed class EndpointInstanceId
    {
        readonly string endpointName;
        readonly string hostId;

        public EndpointInstanceId(string endpointName, string hostId)
        {
            if (endpointName == null)
            {
                throw new ArgumentNullException("endpointName");
            }
            this.endpointName = endpointName;
            this.hostId = hostId;
        }

        /// <summary>
        /// Gets the name of the logical endpoint
        /// </summary>
        public string EndpointName
        {
            get { return endpointName; }
        }

        /// <summary>
        /// Gets the unique ID of the host
        /// </summary>
        public string HostId
        {
            get { return hostId; }
        }

        bool Equals(EndpointInstanceId other)
        {
            return string.Equals(EndpointName, other.EndpointName) && string.Equals(HostId, other.HostId);
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
                return ((EndpointName != null ? EndpointName.GetHashCode() : 0) * 397) ^ (HostId != null ? HostId.GetHashCode() : 0);
            }
        }
    }
}