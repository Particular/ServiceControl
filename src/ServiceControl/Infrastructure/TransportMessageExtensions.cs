namespace ServiceControl
{
    using System;
    using System.Collections.Generic;
    using Infrastructure;
    using NServiceBus;
    using NServiceBus.Faults;

    public static class HeaderExtensions
    {
        public static string ProcessingEndpointName(this IReadOnlyDictionary<string, string> headers)
        {
            string endpoint;

            if (headers.TryGetValue(Headers.ProcessingEndpoint, out endpoint))
            {
                return endpoint;
            }

            // This message could be a failed message.
            if (headers.TryGetValue(FaultsHeaderKeys.FailedQ, out endpoint))
            {
                return Address.Parse(endpoint).Queue;
            }

            // In v5, a message that comes through the Audit Queue
            // has it's ReplyToAddress overwritten to match the processing endpoint
            var replyToAddress = headers.ReplyToAddress();
            if (replyToAddress != null)
            {
                return replyToAddress.Queue;
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
            string existingUniqueMessageId;
            return headers.TryGetValue("ServiceControl.Retry.UniqueMessageId", out existingUniqueMessageId)
                ? existingUniqueMessageId
                : DeterministicGuid.MakeId(headers.MessageId(), headers.ProcessingEndpointName()).ToString();
        }

        // NOTE: Duplicated from TransportMessage
        public static string MessageId(this IReadOnlyDictionary<string, string> headers)
        {
            string str;
            if (headers.TryGetValue(Headers.MessageId, out str))
                return str;
            return default(string);
        }

        // NOTE: Duplicated from TransportMessage
        private static Address ReplyToAddress(this IReadOnlyDictionary<string, string> headers)
        {
            string destination;
            if (headers.TryGetValue(Headers.ReplyToAddress, out destination))
                return Address.Parse(destination);
            return default(Address);
        }

        // NOTE: Duplicated from TransportMessage
        public static MessageIntentEnum MessageIntent(this IReadOnlyDictionary<string, string> headers)
        {
            var messageIntent = default(MessageIntentEnum);

            string messageIntentString;
            if (headers.TryGetValue(Headers.MessageIntent, out messageIntentString))
            {
                Enum.TryParse(messageIntentString, true, out messageIntent);
            }

            return messageIntent;
        }

    }

    public static class TransportMessageExtensions
    {
        public static string ProcessingEndpointName(this TransportMessage message)
        {
            return message.Headers.ProcessingEndpointName();
        }
    }
}