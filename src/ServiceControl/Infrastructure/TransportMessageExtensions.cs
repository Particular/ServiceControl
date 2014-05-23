namespace ServiceControl
{
    using System;
    using Infrastructure;
    using NServiceBus;

    public static class TransportMessageExtensions
    {
        public static string ProcessingEndpointName(this TransportMessage message)
        {
            string endpoint;

            if (message.Headers.TryGetValue(Headers.ProcessingEndpoint, out endpoint))
            {
                return endpoint;
            }

            if (message.ReplyToAddress != null)
            {
                return message.ReplyToAddress.Queue;
            }

            string messageTypes;
            if (!message.Headers.TryGetValue(Headers.EnclosedMessageTypes, out messageTypes))
            {
                messageTypes = "Unknown";
            }

            throw new InvalidOperationException(string.Format("No processing endpoint could be determined for message ({0})", messageTypes));
        }

        public static string UniqueId(this TransportMessage message)
        {
            return DeterministicGuid.MakeId(message.Id, message.ProcessingEndpointName()).ToString();
        }

    }
}