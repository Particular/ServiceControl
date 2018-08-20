﻿namespace ServiceControl
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
            if (headers.TryGetValue(Headers.ProcessingEndpoint, out var endpoint))
            {
                return endpoint;
            }

            // This message could be a failed message.
            if (headers.TryGetValue(FaultsHeaderKeys.FailedQ, out endpoint))
            {
                return ExtractQueue(endpoint);
            }

            // In v5, a message that comes through the Audit Queue
            // has it's ReplyToAddress overwritten to match the processing endpoint
            var replyToAddress = headers.ReplyToAddress();
            if (replyToAddress != null)
            {
                return ExtractQueue(replyToAddress);
            }

            if (headers.TryGetValue(Headers.EnclosedMessageTypes, out var messageTypes))
            {
                throw new Exception($"No processing endpoint could be determined for message ({headers.MessageId()}) with EnclosedMessageTypes ({messageTypes})");
            }

            throw new Exception($"No processing endpoint could be determined for message ({headers.MessageId()})");
        }

        public static string UniqueId(this IReadOnlyDictionary<string, string> headers)
        {
            return headers.TryGetValue("ServiceControl.Retry.UniqueMessageId", out var existingUniqueMessageId)
                ? existingUniqueMessageId
                : DeterministicGuid.MakeId(headers.MessageId(), headers.ProcessingEndpointName()).ToString();
        }

        // NOTE: Duplicated from TransportMessage
        public static string MessageId(this IReadOnlyDictionary<string, string> headers)
        {
            return headers.TryGetValue(Headers.MessageId, out var str) ? str : default;
        }

        // NOTE: Duplicated from TransportMessage

        public static string CorrelationId(this IReadOnlyDictionary<string, string> headers)
        {
            return headers.TryGetValue(Headers.CorrelationId, out var correlationId) ? correlationId : null;
        }

        // NOTE: Duplicated from TransportMessage

        public static MessageIntentEnum MessageIntent(this IReadOnlyDictionary<string, string> headers)
        {
            var messageIntent = default(MessageIntentEnum);

            if (headers.TryGetValue(Headers.MessageIntent, out var messageIntentString))
            {
                Enum.TryParse(messageIntentString, true, out messageIntent);
            }

            return messageIntent;
        }

        static string ReplyToAddress(this IReadOnlyDictionary<string, string> headers)
        {
            return headers.TryGetValue(Headers.ReplyToAddress, out var destination) ? destination : null;
        }

        static string ExtractQueue(string address)
        {
            var atIndex = address?.IndexOf("@", StringComparison.InvariantCulture);

            if (atIndex.HasValue && atIndex.Value > -1)
            {
                return address.Substring(0, atIndex.Value);
            }

            return address;
        }
    }
}