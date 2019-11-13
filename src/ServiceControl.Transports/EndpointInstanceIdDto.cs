namespace ServiceControl.Transports
{
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
}