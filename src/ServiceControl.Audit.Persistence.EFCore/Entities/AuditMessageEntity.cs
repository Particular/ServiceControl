namespace ServiceControl.Audit.Persistence.EFCore.Entities;

using ServiceControl.Audit.Monitoring;

public class AuditMessageEntity
{
    public DateTime CreatedOn { get; set; }
    public long Id { get; set; }
    public Guid UniqueMessageId { get; set; }
    public string? MessageId { get; set; }
    public string? MessageType { get; set; }
    public DateTime? TimeSent { get; set; }
    public DateTime ProcessedAt { get; set; }
    public string? ConversationId { get; set; }
    public bool IsSystemMessage { get; set; }
    public MessageStatus Status { get; set; }
    public string? SendingEndpointName { get; set; }
    public Guid? SendingEndpointHostId { get; set; }
    public string? SendingEndpointHost { get; set; }
    public string? ReceivingEndpointName { get; set; }
    public Guid? ReceivingEndpointHostId { get; set; }
    public string? ReceivingEndpointHost { get; set; }
    public long? CriticalTimeTicks { get; set; }
    public long? ProcessingTimeTicks { get; set; }
    public long? DeliveryTimeTicks { get; set; }
    public required string HeadersJson { get; set; }
    public string? BodyText { get; set; }
    public BodyState BodyState { get; set; }
    public int BodySize { get; set; }
    public string? BodyContentType { get; set; }
}
