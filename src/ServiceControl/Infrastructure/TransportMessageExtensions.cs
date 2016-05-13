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

            // If the ReplyToAddress is null, then the message came from a send-only endpoint. 
            // This message could be a failed message.  
            if (message.Headers.TryGetValue("NServiceBus.FailedQ", out endpoint))
            {
                return endpoint;
            }
            string messageTypes;
            if (message.Headers.TryGetValue(Headers.EnclosedMessageTypes, out messageTypes))
            {
                throw new Exception($"No processing endpoint could be determined for message ({message.Id}) with EnclosedMessageTypes ({messageTypes})");
            }

            throw new Exception($"No processing endpoint could be determined for message ({message.Id})");
        }

        public static string UniqueId(this TransportMessage message)
        {
            return DeterministicGuid.MakeId(message.Id, message.ProcessingEndpointName()).ToString();
        }

    }
}