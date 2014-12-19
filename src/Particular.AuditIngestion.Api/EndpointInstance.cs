namespace Particular.AuditIngestion.Api
{
    using System;

    //TODO: for some messages we don't know host part
    public sealed class EndpointInstance
    {
        public readonly string Endpoint;
        public readonly string Host;

        public EndpointInstance(string endpoint, string host)
        {
            if (endpoint == null)
            {
                throw new ArgumentNullException("endpoint");
            }
            if (host == null)
            {
                throw new ArgumentNullException("host");
            }
            Endpoint = endpoint;
            Host = host;
        }

        bool Equals(EndpointInstance other)
        {
            return string.Equals(Endpoint, other.Endpoint) && string.Equals(Host, other.Host);
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
            return Equals((EndpointInstance) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Endpoint != null ? Endpoint.GetHashCode() : 0)*397) ^ (Host != null ? Host.GetHashCode() : 0);
            }
        }
    }
}