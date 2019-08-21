﻿namespace ServiceControl.Monitoring.Infrastructure
{
    public class EndpointMessageType
    {
        public EndpointMessageType(string endpointName, string messageType)
        {
            EndpointName = endpointName;
            MessageType = messageType;
        }

        public string EndpointName { get; }
        public string MessageType { get; }

        public static EndpointMessageType Unknown(string endpointName)
        {
            return new EndpointMessageType(endpointName, string.Empty);
        }

        protected bool Equals(EndpointMessageType other)
        {
            return string.Equals(EndpointName, other.EndpointName) && string.Equals(MessageType, other.MessageType);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((EndpointMessageType) obj);
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