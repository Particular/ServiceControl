namespace ServiceControl.Monitoring.Infrastructure
{
    using System;

    public readonly struct EndpointMessageType
    {
        public EndpointMessageType(string endpointName, string messageType)
        {
            if (!string.IsNullOrEmpty(messageType))
            {
                // Validate messageType format, will still throw if the format is invalid.
                _ = Type.GetType(messageType, throwOnError: false);
            }

            EndpointName = endpointName;
            MessageType = messageType;
        }

        public string EndpointName { get; }
        public string MessageType { get; }

        public static EndpointMessageType Unknown(string endpointName) => new EndpointMessageType(endpointName, string.Empty);

        bool Equals(EndpointMessageType other) => string.Equals(EndpointName, other.EndpointName) && string.Equals(MessageType, other.MessageType);

        public override bool Equals(object obj) => obj is EndpointMessageType messageType && Equals(messageType);

        public override int GetHashCode() => (EndpointName, MessageType).GetHashCode();
    }
}