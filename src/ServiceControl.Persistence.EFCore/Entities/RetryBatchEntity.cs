namespace ServiceControl.Persistence.EFCore.Entities;

using ServiceControl.Persistence;

public class RetryBatchEntity
{
    public Guid Id { get; set; }

    public RetryBatchStatus Status { get; set; }

    public required string RetrySessionId { get; set; }

    public required string RequestId { get; set; }

    public RetryType RetryType { get; set; }

    public int InitialBatchSize { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime? Last { get; set; }

    public string? StagingId { get; set; }

    public string? Context { get; set; }

    public string? Originator { get; set; }

    public string? Classifier { get; set; }

    public string? InitiatedById { get; set; }

    public string? InitiatedByName { get; set; }

    public string? OperationId { get; set; }
}
