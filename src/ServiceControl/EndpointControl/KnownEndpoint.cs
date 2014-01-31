namespace ServiceControl.EndpointControl
{
    using System.Collections.Generic;

    public class KnownEndpoint
    {
        public KnownEndpoint()
        {
            Machines = new List<string>();
        }
        protected bool Equals(KnownEndpoint other)
        {
            return string.Equals(Id, other.Id);
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
            return Equals((KnownEndpoint) obj);
        }

        public override int GetHashCode()
        {
            return (Id != null ? Id.GetHashCode() : 0);
        }

        public string Id { get; set; }
        public string Name { get; set; }
        public List<string> Machines { get; set; }
    }
}