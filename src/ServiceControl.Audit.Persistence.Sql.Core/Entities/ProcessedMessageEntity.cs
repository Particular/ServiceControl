namespace ServiceControl.Audit.Persistence.Sql.Core.Entities;

public class ProcessedMessageEntity
{
    public Guid Id { get; set; }
    public string UniqueMessageId { get; set; } = null!;

    // JSON columns for complex nested data
    public string MessageMetadataJson { get; set; } = null!;
    public string HeadersJson { get; set; } = null!;

    // Full-text search column (populated from headers and body)
    public string? Body { get; set; }

    // Denormalized fields for efficient querying
    public string? MessageId { get; set; }
    public string? MessageType { get; set; }
    public DateTime? TimeSent { get; set; }
    public DateTime ProcessedAt { get; set; }
    public bool IsSystemMessage { get; set; }
    public bool IsRetried { get; set; }
    public string? ConversationId { get; set; }
    public int MessageIntent { get; set; }

    // Endpoint details (denormalized from MessageMetadata)
    public string? SendingEndpointName { get; set; }
    public string? ReceivingEndpointName { get; set; }

    // Performance metrics (stored as ticks for precision)
    public long? CriticalTimeTicks { get; set; }
    public long? ProcessingTimeTicks { get; set; }
    public long? DeliveryTimeTicks { get; set; }

    // Body storage info
    public int BodySize { get; set; }
    public string? BodyUrl { get; set; }
    public bool BodyNotStored { get; set; }

    // Saga information (stored as JSON for flexibility)
    public string? InvokedSagasJson { get; set; }
    public string? OriginatesFromSagaJson { get; set; }

    // Retention
    public DateTime ExpiresAt { get; set; }
}
