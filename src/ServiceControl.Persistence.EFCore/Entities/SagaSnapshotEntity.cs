namespace ServiceControl.Persistence.EFCore.Entities;

using ServiceControl.SagaAudit;

public class SagaSnapshotEntity
{
    /// <summary>
    /// Ingestion time truncated to the hour. The PostgreSQL partition key.
    /// </summary>
    public DateTime CreatedOn { get; set; }

    /// <summary>
    /// A database generated identity. Snapshots carry no natural key and nothing reads this back, so
    /// it exists only because a partitioned table's primary key must include the partition key and
    /// created_on alone is not unique. A redelivered saga audit message produces a second row.
    /// </summary>
    public long Id { get; set; }

    public Guid SagaId { get; set; }

    public string? SagaType { get; set; }

    public SagaStateChangeStatus Status { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime FinishTime { get; set; }

    public DateTime ProcessedAt { get; set; }

    public string? Endpoint { get; set; }

    public string? StateAfterChange { get; set; }

    public string? InitiatingMessageJson { get; set; }

    public string? OutgoingMessagesJson { get; set; }
}
