namespace ServiceControl.Audit.Persistence.Sql.Core.Entities;

public class ProcessedMessageEntity
{
    public long Id { get; set; }
    public Guid BatchId { get; set; }
    public string UniqueMessageId { get; set; } = null!;

    // JSON columns for complex nested data
    public string HeadersJson { get; set; } = null!;

    // Full-text search column (combines headers JSON and message body for indexing)
    public string? SearchableContent { get; set; }

    // Denormalized fields for efficient querying
    public string? MessageId { get; set; }
    public string? MessageType { get; set; }
    public DateTime? TimeSent { get; set; }
    public DateTime ProcessedAt { get; set; }
    public bool IsSystemMessage { get; set; }
    public int Status { get; set; }
    public string? ConversationId { get; set; }

    // Endpoint details (denormalized from MessageMetadata)
    public string? ReceivingEndpointName { get; set; }

    // Performance metrics (stored as ticks for precision)
    public long? CriticalTimeTicks { get; set; }
    public long? ProcessingTimeTicks { get; set; }
    public long? DeliveryTimeTicks { get; set; }

    // Body storage info
    public int BodySize { get; set; }
    public string? BodyUrl { get; set; }
    public bool BodyNotStored { get; set; }
}
