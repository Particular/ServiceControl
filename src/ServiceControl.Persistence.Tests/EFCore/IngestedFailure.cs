namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Text;
using NServiceBus.Extensibility;
using NServiceBus.Transport;
using ServiceControl.Contracts.Operations;
using ServiceControl.MessageFailures;
using ServiceControl.Operations;
using ServiceControl.Persistence.Infrastructure;

class IngestedFailure
{
    public string MessageId { get; init; } = Guid.NewGuid().ToString();
    public string EndpointName { get; init; } = "Sales";
    public string ContentType { get; init; } = "text/xml";
    public byte[] Body { get; init; } = Encoding.UTF8.GetBytes("<order>1</order>");
    public DateTime AttemptedAt { get; init; } = new(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc);
    public DateTime TimeOfFailure { get; init; } = new(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc);
    public DateTime TimeSent { get; init; } = new(2026, 7, 22, 9, 59, 0, DateTimeKind.Utc);
    public string MessageType { get; init; } = "MyCompany.Sales.OrderPlaced";
    public string ConversationId { get; init; } = Guid.NewGuid().ToString();
    public string QueueAddress { get; init; } = "error";
    public string ExceptionType { get; init; } = "System.InvalidOperationException";
    public string ExceptionMessage { get; init; } = "Something went wrong";
    public bool IsSystemMessage { get; init; }
    public EndpointDetails SendingEndpoint { get; init; } = new() { Name = "Ordering", Host = "SenderHost", HostId = Guid.NewGuid() };
    public EndpointDetails ReceivingEndpoint { get; init; } = new() { Name = "Sales", Host = "ReceiverHost", HostId = Guid.NewGuid() };
    public List<FailedMessage.FailureGroup> Groups { get; init; } =
    [
        new() { Id = Guid.NewGuid().ToString(), Title = "OrderPlaced", Type = "Message Type" }
    ];

    public Dictionary<string, string> Headers => field ??= new Dictionary<string, string>
    {
        [NServiceBus.Headers.MessageId] = MessageId,
        [NServiceBus.Headers.ProcessingEndpoint] = EndpointName,
        [NServiceBus.Headers.ContentType] = ContentType,
        [NServiceBus.Headers.EnclosedMessageTypes] = MessageType,
        ["NServiceBus.FailedQ"] = QueueAddress,
        ["NServiceBus.ExceptionInfo.ExceptionType"] = ExceptionType,
        ["NServiceBus.ExceptionInfo.Message"] = ExceptionMessage
    };

    public string UniqueMessageIdString => Headers.UniqueId();

    public Guid UniqueMessageId => Guid.Parse(UniqueMessageIdString);

    public MessageContext Context =>
        new(MessageId, Headers, Body, new TransportTransaction(), "receiveAddress", new ContextBag());

    public FailedMessage.ProcessingAttempt ProcessingAttempt => new()
    {
        AttemptedAt = AttemptedAt,
        MessageId = MessageId,
        Headers = Headers,
        MessageMetadata = new Dictionary<string, object>
        {
            ["MessageId"] = MessageId,
            ["MessageType"] = MessageType,
            ["TimeSent"] = TimeSent,
            ["ConversationId"] = ConversationId,
            ["IsSystemMessage"] = IsSystemMessage,
            ["SendingEndpoint"] = SendingEndpoint,
            ["ReceivingEndpoint"] = ReceivingEndpoint
        },
        FailureDetails = new FailureDetails
        {
            TimeOfFailure = TimeOfFailure,
            AddressOfFailingEndpoint = QueueAddress,
            Exception = new ExceptionDetails
            {
                ExceptionType = ExceptionType,
                Message = ExceptionMessage
            }
        }
    };

    public IngestedFailure NextAttempt(DateTime attemptedAt) => new()
    {
        MessageId = MessageId,
        EndpointName = EndpointName,
        AttemptedAt = attemptedAt,
        TimeOfFailure = attemptedAt,
        ContentType = ContentType,
        Body = Body,
        MessageType = MessageType,
        ConversationId = ConversationId,
        QueueAddress = QueueAddress,
        ExceptionType = ExceptionType,
        ExceptionMessage = ExceptionMessage,
        IsSystemMessage = IsSystemMessage,
        SendingEndpoint = SendingEndpoint,
        ReceivingEndpoint = ReceivingEndpoint,
        TimeSent = TimeSent,
        Groups = Groups
    };
}
