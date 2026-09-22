namespace ServiceControl.Persistence.EFCore.Implementation.Audit;

/// <summary>
/// The projection both message tables are queried through, so that one statement can union,
/// sort, page and count failed and audited messages. Every sortable column of the message views is
/// a member, and the status is resolved to what the view reports so that a sort by status orders
/// both kinds of row the same way.
/// </summary>
public sealed class MessageRow
{
    public Guid UniqueMessageId { get; init; }
    public bool IsAudit { get; init; }
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
    public long CriticalTimeTicks { get; init; }
    public long ProcessingTimeTicks { get; init; }
    public long DeliveryTimeTicks { get; init; }
    public string HeadersJson { get; init; } = string.Empty;
    public int BodySize { get; init; }

    /// <summary>
    /// What changes when the row changes: the last modification for a failed message, and the
    /// ingestion hour for an audit row, which is never modified.
    /// </summary>
    public DateTime Version { get; init; }

    /// <summary>
    /// The attempt count for a failed message, the identity for an audit row. With
    /// <see cref="Version"/> it makes the ETag move whenever the view would.
    /// </summary>
    public long Revision { get; init; }
}
