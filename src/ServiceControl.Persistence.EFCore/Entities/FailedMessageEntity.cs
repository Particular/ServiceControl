namespace ServiceControl.Persistence.EFCore.Entities;

using ServiceControl.MessageFailures;

public class FailedMessageEntity
{
    public Guid UniqueMessageId { get; set; }

    public FailedMessageStatus Status { get; set; }

    public DateTime StatusChangedAt { get; set; }

    public DateTime LastModified { get; set; }

    public int NumberOfProcessingAttempts { get; set; }

    public DateTime FirstTimeOfFailure { get; set; }

    public DateTime LastTimeOfFailure { get; set; }

    public DateTime LastAttemptedAt { get; set; }

    public string? MessageId { get; set; }

    public string? MessageType { get; set; }

    public DateTime? TimeSent { get; set; }

    public string? ConversationId { get; set; }

    public string? QueueAddress { get; set; }

    public string? SendingEndpointName { get; set; }

    public Guid? SendingEndpointHostId { get; set; }

    public string? SendingEndpointHost { get; set; }

    public string? ReceivingEndpointName { get; set; }

    public Guid? ReceivingEndpointHostId { get; set; }

    public string? ReceivingEndpointHost { get; set; }

    public string? ExceptionType { get; set; }

    public string? ExceptionMessage { get; set; }

    public bool IsSystemMessage { get; set; }

    public required string HeadersJson { get; set; }

    public string? BodyText { get; set; }

    public bool BodyStoredExternally { get; set; }

    public int BodySize { get; set; }

    public string? BodyContentType { get; set; }

    public required string FailingEndpointAddress { get; set; }
}
