namespace ServiceControl.Monitoring.Infrastructure
{
    public readonly struct EndpointMessageType
    {
        public EndpointMessageType(string endpointName, string enclosedMessageTypes)
        {
            var index = (enclosedMessageTypes ?? "").IndexOf(';');

            var firstType = index != -1
                ? enclosedMessageTypes.Substring(0, index)
                : enclosedMessageTypes;

            EndpointName = endpointName;
            MessageType = firstType;
        }

        public string EndpointName { get; }
        public string MessageType { get; }

        public static EndpointMessageType Unknown(string endpointName) => new EndpointMessageType(endpointName, string.Empty);

        bool Equals(EndpointMessageType other) => string.Equals(EndpointName, other.EndpointName) && string.Equals(MessageType, other.MessageType);

        public override bool Equals(object obj) => obj is EndpointMessageType messageType && Equals(messageType);

        public override int GetHashCode() => (EndpointName, MessageType).GetHashCode();
    }
}