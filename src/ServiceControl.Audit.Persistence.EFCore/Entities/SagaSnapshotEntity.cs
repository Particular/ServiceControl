namespace ServiceControl.Audit.Persistence.EFCore.Entities;

using ServiceControl.SagaAudit;

public class SagaSnapshotEntity
{
    public DateTime CreatedOn { get; set; }
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
