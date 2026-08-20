namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Text;
using NServiceBus;
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
    public DateTime? TimeSent { get; init; } = new(2026, 7, 22, 9, 59, 0, DateTimeKind.Utc);
    public MessageIntent MessageIntent { get; init; } = MessageIntent.Send;
    public string MessageType { get; init; } = "MyCompany.Sales.OrderPlaced";
    public string ConversationId { get; init; } = Guid.NewGuid().ToString();
    public string QueueAddress { get; init; } = "error";
    public string FailingEndpointAddress { get; init; }
    public string ExceptionType { get; init; } = "System.InvalidOperationException";
    public string ExceptionMessage { get; init; } = "Something went wrong";
    public string ExceptionSource { get; init; }
    public string ExceptionStackTrace { get; init; }
    public string EditOf { get; init; }
    public bool IsSystemMessage { get; init; }
    public EndpointDetails SendingEndpoint { get; init; } = new() { Name = "Ordering", Host = "SenderHost", HostId = Guid.NewGuid() };
    public EndpointDetails ReceivingEndpoint { get; init; } = new() { Name = "Sales", Host = "ReceiverHost", HostId = Guid.NewGuid() };
    public List<FailedMessage.FailureGroup> Groups { get; init; } =
    [
        new() { Id = Guid.NewGuid().ToString(), Title = "OrderPlaced", Type = "Message Type" }
    ];

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
            ["NServiceBus.FailedQ"] = QueueAddress,
            ["NServiceBus.ExceptionInfo.ExceptionType"] = ExceptionType,
            ["NServiceBus.ExceptionInfo.Message"] = ExceptionMessage
        };

        if (ExceptionSource != null)
        {
            headers["NServiceBus.ExceptionInfo.Source"] = ExceptionSource;
        }

        if (ExceptionStackTrace != null)
        {
            headers["NServiceBus.ExceptionInfo.StackTrace"] = ExceptionStackTrace;
        }

        if (EditOf != null)
        {
            headers["ServiceControl.EditOf"] = EditOf;
        }

        return headers;
    }

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
            AddressOfFailingEndpoint = FailingEndpointAddress ?? QueueAddress,
            Exception = new ExceptionDetails
            {
                ExceptionType = ExceptionType,
                Message = ExceptionMessage,
                Source = ExceptionSource,
                StackTrace = ExceptionStackTrace
            }
        }
    };

    public IngestedFailure NextAttempt(DateTime attemptedAt, List<FailedMessage.FailureGroup> groups = null) => new()
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
        FailingEndpointAddress = FailingEndpointAddress,
        ExceptionType = ExceptionType,
        ExceptionMessage = ExceptionMessage,
        ExceptionSource = ExceptionSource,
        ExceptionStackTrace = ExceptionStackTrace,
        EditOf = EditOf,
        IsSystemMessage = IsSystemMessage,
        SendingEndpoint = SendingEndpoint,
        ReceivingEndpoint = ReceivingEndpoint,
        TimeSent = TimeSent,
        Groups = groups ?? Groups
    };

    /// <summary>
    /// The same failure as a stored document, for seeding through
    /// <see cref="IPersistenceTestsContext.InsertFailedMessages" /> rather than through ingestion.
    /// </summary>
    public FailedMessage ToFailedMessage(FailedMessageStatus status = FailedMessageStatus.Unresolved, int numberOfAttempts = 1)
    {
        var attempts = new List<FailedMessage.ProcessingAttempt>();

        // Earlier attempts only have to be distinct in AttemptedAt, which is what the attempt count
        // is derived from. Their content is never read.
        for (var i = numberOfAttempts - 1; i > 0; i--)
        {
            attempts.Add(NextAttempt(AttemptedAt.AddMinutes(-i)).ProcessingAttempt);
        }

        attempts.Add(ProcessingAttempt);

        // Id is left for the caller to stamp through IPersistenceTestsContext, because RavenDB
        // stores the document under it while the relational persisters ignore it.
        return new FailedMessage
        {
            Id = Guid.NewGuid().ToString(),
            UniqueMessageId = UniqueMessageIdString,
            Status = status,
            ProcessingAttempts = attempts,
            FailureGroups = Groups
        };
    }
}
