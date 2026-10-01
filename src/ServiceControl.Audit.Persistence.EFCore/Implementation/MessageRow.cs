namespace ServiceControl.Audit.Persistence.EFCore.Implementation;

using ServiceControl.Audit.Monitoring;
using ServiceControl.Audit.Persistence.EFCore.Entities;

sealed class MessageRow
{
    public DateTime CreatedOn { get; init; }
    public long Id { get; init; }
    public Guid UniqueMessageId { get; init; }
    public string? MessageId { get; init; }
    public string? MessageType { get; init; }
    public DateTime? TimeSent { get; init; }
    public DateTime ProcessedAt { get; init; }
    public string? ConversationId { get; init; }
    public bool IsSystemMessage { get; init; }
    public MessageStatus Status { get; init; }
    public string? SendingEndpointName { get; init; }
    public Guid? SendingEndpointHostId { get; init; }
    public string? SendingEndpointHost { get; init; }
    public string? ReceivingEndpointName { get; init; }
    public Guid? ReceivingEndpointHostId { get; init; }
    public string? ReceivingEndpointHost { get; init; }
    public long? CriticalTimeTicks { get; init; }
    public long? ProcessingTimeTicks { get; init; }
    public long? DeliveryTimeTicks { get; init; }
    public string HeadersJson { get; init; } = string.Empty;
    public BodyState BodyState { get; init; }
    public int BodySize { get; init; }
}
