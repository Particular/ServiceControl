namespace ServiceControl.Audit.Persistence.Sql.Core.Entities;

using ServiceControl.SagaAudit;

public class SagaSnapshotEntity
{
    public long Id { get; set; }
    public Guid SagaId { get; set; }
    public string? SagaType { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime FinishTime { get; set; }
    public SagaStateChangeStatus Status { get; set; }
    public string? StateAfterChange { get; set; }
    public string? InitiatingMessageJson { get; set; }
    public string? OutgoingMessagesJson { get; set; }
    public string? Endpoint { get; set; }
    public DateTime ProcessedAt { get; set; }
}
