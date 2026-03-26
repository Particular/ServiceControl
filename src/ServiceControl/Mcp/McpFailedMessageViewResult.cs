#nullable enable

namespace ServiceControl.Mcp;

using System;
using ServiceControl.Contracts.Operations;
using ServiceControl.MessageFailures;
using MessageFailures.Api;
using ServiceControl.Operations;

public class McpFailedMessageViewResult
{
    public string? Error { get; init; }
    public string Id { get; init; } = string.Empty;
    public string MessageType { get; init; } = string.Empty;
    public DateTime? TimeSent { get; init; }
    public bool IsSystemMessage { get; init; }
    public ExceptionDetails? Exception { get; init; }
    public string MessageId { get; init; } = string.Empty;
    public int NumberOfProcessingAttempts { get; init; }
    public FailedMessageStatus Status { get; init; }
    public EndpointDetails? SendingEndpoint { get; init; }
    public EndpointDetails? ReceivingEndpoint { get; init; }
    public string QueueAddress { get; init; } = string.Empty;
    public DateTime TimeOfFailure { get; init; }
    public DateTime LastModified { get; init; }
    public bool Edited { get; init; }
    public string EditOf { get; init; } = string.Empty;

    public static McpFailedMessageViewResult From(FailedMessageView message)
    {
        return new McpFailedMessageViewResult
        {
            Id = message.Id,
            MessageType = message.MessageType,
            TimeSent = message.TimeSent,
            IsSystemMessage = message.IsSystemMessage,
            Exception = message.Exception,
            MessageId = message.MessageId,
            NumberOfProcessingAttempts = message.NumberOfProcessingAttempts,
            Status = message.Status,
            SendingEndpoint = message.SendingEndpoint,
            ReceivingEndpoint = message.ReceivingEndpoint,
            QueueAddress = message.QueueAddress,
            TimeOfFailure = message.TimeOfFailure,
            LastModified = message.LastModified,
            Edited = message.Edited,
            EditOf = message.EditOf
        };
    }
}
