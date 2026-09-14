namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Text;
using NServiceBus;
using ServiceControl.MessageAuditing;
using ServiceControl.Operations;
using ServiceControl.Persistence.Infrastructure;

/// <summary>
/// An audit message the way the audit pipeline hands it to the unit of work: the headers an endpoint
/// wrote, and the metadata the enrichers derive from them.
/// </summary>
class IngestedAudit
{
    public string MessageId { get; init; } = Guid.NewGuid().ToString();
    public string EndpointName { get; init; } = "Sales";
    public string ContentType { get; init; } = "text/xml";
    public byte[] Body { get; init; } = Encoding.UTF8.GetBytes("<order>1</order>");
    public DateTime TimeSent { get; init; } = new(2026, 7, 22, 9, 59, 0, DateTimeKind.Utc);
    public DateTime ProcessingStarted { get; init; } = new(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc);
    public DateTime ProcessingEnded { get; init; } = new(2026, 7, 22, 10, 0, 1, DateTimeKind.Utc);
    public MessageIntent MessageIntent { get; init; } = MessageIntent.Send;
    public string MessageType { get; init; } = "MyCompany.Sales.OrderPlaced";
    public string ConversationId { get; init; } = Guid.NewGuid().ToString();
    public bool IsSystemMessage { get; init; }
    public string RetryOf { get; init; }
    public EndpointDetails SendingEndpoint { get; init; } = new() { Name = "Ordering", Host = "SenderHost", HostId = Guid.NewGuid() };
    public EndpointDetails ReceivingEndpoint { get; init; } = new() { Name = "Sales", Host = "ReceiverHost", HostId = Guid.NewGuid() };

    public Dictionary<string, string> Headers => field ??= BuildHeaders();

    Dictionary<string, string> BuildHeaders()
    {
        var headers = new Dictionary<string, string>
        {
            [NServiceBus.Headers.MessageId] = MessageId,
            [NServiceBus.Headers.ProcessingEndpoint] = EndpointName,
            [NServiceBus.Headers.ContentType] = ContentType,
            [NServiceBus.Headers.EnclosedMessageTypes] = MessageType,
            [NServiceBus.Headers.MessageIntent] = MessageIntent.ToString(),
            [NServiceBus.Headers.ConversationId] = ConversationId,
            [NServiceBus.Headers.TimeSent] = DateTimeOffsetHelper.ToWireFormattedString(new DateTimeOffset(TimeSent)),
            [NServiceBus.Headers.ProcessingStarted] = DateTimeOffsetHelper.ToWireFormattedString(new DateTimeOffset(ProcessingStarted)),
            [NServiceBus.Headers.ProcessingEnded] = DateTimeOffsetHelper.ToWireFormattedString(new DateTimeOffset(ProcessingEnded)),
            [NServiceBus.Headers.OriginatingEndpoint] = SendingEndpoint.Name,
            [NServiceBus.Headers.OriginatingMachine] = SendingEndpoint.Host,
            [NServiceBus.Headers.OriginatingHostId] = SendingEndpoint.HostId.ToString(),
            [NServiceBus.Headers.HostId] = ReceivingEndpoint.HostId.ToString(),
            [NServiceBus.Headers.HostDisplayName] = ReceivingEndpoint.Host
        };

        if (RetryOf != null)
        {
            headers["ServiceControl.Retry.UniqueMessageId"] = RetryOf;
        }

        return headers;
    }

    public Dictionary<string, object> Metadata => new()
    {
        ["MessageId"] = MessageId,
        ["MessageIntent"] = MessageIntent,
        ["MessageType"] = MessageType,
        ["IsSystemMessage"] = IsSystemMessage,
        ["TimeSent"] = TimeSent,
        ["ConversationId"] = ConversationId,
        ["SendingEndpoint"] = SendingEndpoint,
        ["ReceivingEndpoint"] = ReceivingEndpoint,
        ["CriticalTime"] = ProcessingEnded - TimeSent,
        ["ProcessingTime"] = ProcessingEnded - ProcessingStarted,
        ["DeliveryTime"] = ProcessingStarted - TimeSent,
        ["IsRetried"] = RetryOf != null
    };

    public ProcessedMessage ToProcessedMessage() => new(Headers, Metadata);

    public string UniqueMessageIdString => Headers.UniqueId();

    public Guid UniqueMessageId => Guid.Parse(UniqueMessageIdString);
}
