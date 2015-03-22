namespace Particular.Operations.Ingestion.Api
{
    using System;

    //TODO: Add version here


    /// <summary>
    /// Identifies a physical endpoint instance.
    /// </summary>
    public sealed class EndpointInstance
    {
        readonly string endpointName;
        readonly string hostId;

        public static EndpointInstance Unknown = new EndpointInstance("Unknown", "Unknown");

        public EndpointInstance(string endpointName, string hostId)
        {
            if (endpointName == null)
            {
                throw new ArgumentNullException("endpointName");
            }
            if (hostId == null)
            {
                throw new ArgumentNullException("hostId");
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

        bool Equals(EndpointInstance other)
        {
            return string.Equals(endpointName, other.endpointName) && string.Equals(hostId, other.hostId);
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
            return obj is EndpointInstance && Equals((EndpointInstance) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((endpointName != null ? endpointName.GetHashCode() : 0)*397) ^ (hostId != null ? hostId.GetHashCode() : 0);
            }
        }

        public static bool operator ==(EndpointInstance left, EndpointInstance right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(EndpointInstance left, EndpointInstance right)
        {
            return !Equals(left, right);
        }

        public override string ToString()
        {
            return string.Format("{0}/{1}", endpointName, hostId ?? "?");
        }
    }
}