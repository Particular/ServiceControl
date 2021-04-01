namespace ServiceControl.Monitoring.Infrastructure
{
    public readonly struct EndpointMessageType
    {
        public EndpointMessageType(string endpointName, string messageType)
        {
            EndpointName = endpointName;
            MessageType = messageType;
        }

        public string EndpointName { get; }
        public string MessageType { get; }

        public static EndpointMessageType Unknown(string endpointName) => new EndpointMessageType(endpointName, string.Empty);

        bool Equals(EndpointMessageType other) => string.Equals(EndpointName, other.EndpointName) && string.Equals(MessageType, other.MessageType);

        public override bool Equals(object obj)
        {
            if (obj is null)
            {
                return false;
            }

            if (obj.GetType() != GetType())
            {
                return false;
            }

            return Equals((EndpointMessageType)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((EndpointName != null ? EndpointName.GetHashCode() : 0) * 397) ^ (MessageType != null ? MessageType.GetHashCode() : 0);
            }
        }
    }
}