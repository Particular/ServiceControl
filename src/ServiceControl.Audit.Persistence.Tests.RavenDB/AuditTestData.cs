namespace ServiceControl.Audit.Persistence.Tests;

using System;
using System.Collections.Generic;
using Auditing;
using Monitoring;
using NServiceBus;

static class AuditTestData
{
    // Carries every metadata entry that the audit indexes read, as the audit ingestion does.
    public static ProcessedMessage CreateProcessedMessage(
        string messageId = null,
        string messageType = "MyMessageType",
        string conversationId = null,
        string receivingEndpoint = "ReceivingEndpoint",
        DateTime? timeSent = null,
        TimeSpan? criticalTime = null,
        TimeSpan? processingTime = null,
        TimeSpan? deliveryTime = null,
        DateTimeOffset? processedAt = null)
    {
        messageId ??= Guid.NewGuid().ToString();
        conversationId ??= Guid.NewGuid().ToString();
        timeSent ??= DateTime.UtcNow;
        processedAt ??= DateTimeOffset.UtcNow;

        var metadata = new Dictionary<string, object>
        {
            { "MessageId", messageId },
            { "MessageIntent", MessageIntent.Send },
            { "TimeSent", timeSent.Value },
            { "CriticalTime", criticalTime ?? TimeSpan.FromSeconds(5) },
            { "ProcessingTime", processingTime ?? TimeSpan.FromSeconds(1) },
            { "DeliveryTime", deliveryTime ?? TimeSpan.FromSeconds(4) },
            { "IsSystemMessage", false },
            { "MessageType", messageType },
            { "IsRetried", false },
            { "ConversationId", conversationId },
            { "SendingEndpoint", new EndpointDetails { Name = "SendingEndpoint", HostId = Guid.Empty, Host = "host" } },
            { "ReceivingEndpoint", new EndpointDetails { Name = receivingEndpoint, HostId = Guid.Empty, Host = "host" } }
        };

        var headers = new Dictionary<string, string>
        {
            { Headers.MessageId, messageId },
            { Headers.ProcessingEndpoint, receivingEndpoint },
            { Headers.MessageIntent, MessageIntent.Send.ToString() },
            { Headers.ConversationId, conversationId },
            { Headers.ProcessingStarted, DateTimeOffsetHelper.ToWireFormattedString(processedAt.Value) },
            { Headers.ProcessingEnded, DateTimeOffsetHelper.ToWireFormattedString(processedAt.Value) },
            { Headers.EnclosedMessageTypes, messageType }
        };

        return new ProcessedMessage(headers, metadata);
    }
}
