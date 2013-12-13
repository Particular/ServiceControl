namespace ServiceControl
{
    using System;
    using Infrastructure;
    using NServiceBus;

    public static class TransportMessageExtentions
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

            throw new InvalidOperationException("No processing endpoint could be determined for message");
        }

        public static string UniqueId(this TransportMessage message)
        {
            return DeterministicGuid.MakeId(message.Id, message.ProcessingEndpointName()).ToString();
        }

    }
}