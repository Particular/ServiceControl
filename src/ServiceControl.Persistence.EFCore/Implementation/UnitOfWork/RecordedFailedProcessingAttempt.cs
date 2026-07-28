namespace ServiceControl.Persistence.EFCore.Implementation.UnitOfWork;

using ServiceControl.MessageFailures;

sealed class RecordedFailedProcessingAttempt
{
    public required Guid UniqueMessageId { get; init; }
    public required DateTime AttemptedAt { get; init; }
    public required DateTime TimeOfFailure { get; init; }
    public required IReadOnlyList<FailedMessage.FailureGroup> Groups { get; init; }
    public required string HeadersJson { get; init; }

    public string? MessageId { get; init; }
    public string? MessageType { get; init; }
    public DateTime? TimeSent { get; init; }
    public string? ConversationId { get; init; }
    public string? QueueAddress { get; init; }
    public string? SendingEndpointName { get; init; }
    public Guid? SendingEndpointHostId { get; init; }
    public string? SendingEndpointHost { get; init; }
    public string? ReceivingEndpointName { get; init; }
    public Guid? ReceivingEndpointHostId { get; init; }
    public string? ReceivingEndpointHost { get; init; }
    public string? ExceptionType { get; init; }
    public string? ExceptionMessage { get; init; }
    public bool IsSystemMessage { get; init; }

    public string? BodyText { get; init; }
    public bool BodyStoredExternally { get; init; }
    public int BodySize { get; init; }
    public string? BodyContentType { get; init; }
    public required string FailingEndpointAddress { get; set; }
}
