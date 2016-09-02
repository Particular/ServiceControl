namespace ServiceControl
{
    using System;
    using System.Collections.Generic;
    using Infrastructure;
    using NServiceBus;

    public static class TransportMessageExtensions
    {
        public static string ProcessingEndpointName(this TransportMessage message) => message.Headers.ProcessingEndpointName();

        public static string ProcessingEndpointName(this IReadOnlyDictionary<string, string> headers)
        {
            string endpoint;

            if (headers.TryGetValue(Headers.ProcessingEndpoint, out endpoint))
            {
                return endpoint;
            }

            string replyToAddress;
            if (headers.TryGetValue(Headers.ReplyToAddress, out replyToAddress))
            {
                return Address.Parse(replyToAddress).Queue;
            }

            // If the ReplyToAddress is null, then the message came from a send-only endpoint.
            // This message could be a failed message.
            if (headers.TryGetValue("NServiceBus.FailedQ", out endpoint))
            {
                return endpoint;
            }

            var messageId = headers[Headers.MessageId];

            string messageTypes;
            if (headers.TryGetValue(Headers.EnclosedMessageTypes, out messageTypes))
            {
                throw new Exception($"No processing endpoint could be determined for message ({messageId}) with EnclosedMessageTypes ({messageTypes})");
            }

            throw new Exception($"No processing endpoint could be determined for message ({messageId})");
        }

        public static string UniqueId(this TransportMessage message)
        {
            return DeterministicGuid.MakeId(message.Id, message.ProcessingEndpointName()).ToString();
        }

        public static string UniqueMessageId(this IReadOnlyDictionary<string, string> headers) =>
            DeterministicGuid.MakeId(headers[Headers.MessageId], headers.ProcessingEndpointName()).ToString();
    }
}