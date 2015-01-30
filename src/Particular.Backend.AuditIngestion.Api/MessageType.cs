namespace Particular.Operations.Ingestion.Api
{
    public class MessageType
    {
        readonly string name;
        readonly bool isSystem;

        public static MessageType Unknown = new MessageType("Unknown",false);
        public static MessageType Control = new MessageType("ControlMessage", true);

        public MessageType(string name, bool isSystem)
        {
            this.name = name;
            this.isSystem = isSystem;
        }

        public string Name
        {
            get { return name; }
        }

        public bool IsSystem
        {
            get { return isSystem; }
        }

        protected bool Equals(MessageType other)
        {
            return string.Equals(name, other.name) && isSystem.Equals(other.isSystem);
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
            return Equals((MessageType) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((name != null ? name.GetHashCode() : 0)*397) ^ isSystem.GetHashCode();
            }
        }
    }
}