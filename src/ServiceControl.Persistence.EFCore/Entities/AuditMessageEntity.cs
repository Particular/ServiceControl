namespace ServiceControl.Persistence.EFCore.Entities;

using ServiceControl.Persistence;

public class AuditMessageEntity
{
    /// <summary>
    /// Ingestion time truncated to the hour. The PostgreSQL partition key, and therefore part of the
    /// primary key, because a partitioned table's unique constraints must include it.
    /// </summary>
    public DateTime CreatedOn { get; set; }

    /// <summary>
    /// A database generated identity. Audit rows are immutable inserts and nothing reads this back, so
    /// it exists only because a partitioned table's primary key must include the partition key and
    /// created_on alone is not unique. Rows are not deduplicated, so a redelivered audit message
    /// produces a second row.
    /// </summary>
    public long Id { get; set; }

    public Guid UniqueMessageId { get; set; }

    public string? MessageId { get; set; }

    public string? MessageType { get; set; }

    public DateTime? TimeSent { get; set; }

    /// <summary>
    /// What the message view reports as ProcessedAt, taken from the ProcessingEnded header. Unrelated
    /// to <see cref="CreatedOn"/>, which is when this instance happened to ingest it.
    /// </summary>
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

    public bool BodyStoredExternally { get; set; }

    public int BodySize { get; set; }

    public string? BodyContentType { get; set; }
}
