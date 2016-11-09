namespace ServiceControl
{
    using System;
    using System.Collections.Generic;
    using Infrastructure;
    using NServiceBus;

    public static class HeaderExtensions
    {
        public static string ProcessingEndpointName(this IReadOnlyDictionary<string, string> headers)
        {
            string endpoint;

            if (headers.TryGetValue(Headers.ProcessingEndpoint, out endpoint))
            {
                return endpoint;
            }

            var replyToAddress = headers.ReplyToAddress();
            if (replyToAddress != null)
            {
                return replyToAddress.Queue;
            }

            // If the ReplyToAddress is null, then the message came from a send-only endpoint.
            // This message could be a failed message.
            if (headers.TryGetValue("NServiceBus.FailedQ", out endpoint))
            {
                return endpoint;
            }
            string messageTypes;
            if (headers.TryGetValue(Headers.EnclosedMessageTypes, out messageTypes))
            {
                throw new Exception($"No processing endpoint could be determined for message ({headers.MessageId()}) with EnclosedMessageTypes ({messageTypes})");
            }

            throw new Exception($"No processing endpoint could be determined for message ({headers.MessageId()})");

        }

        public static string UniqueId(this IReadOnlyDictionary<string, string> headers)
        {
            return DeterministicGuid.MakeId(headers.MessageId(), headers.ProcessingEndpointName()).ToString();
        }

        // NOTE: Duplicated from TransportMessage
        public static string MessageId(this IReadOnlyDictionary<string, string> headers)
        {
            string str;
            if (headers.TryGetValue("NServiceBus.MessageId", out str))
                return str;
            return default(string);
        }

        // NOTE: Duplicated from TransportMessage
        private static Address ReplyToAddress(this IReadOnlyDictionary<string, string> headers)
        {
            string destination;
            if (headers.TryGetValue("NServiceBus.ReplyToAddress", out destination))
                return Address.Parse(destination);
            return default(Address);
        }

    }

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