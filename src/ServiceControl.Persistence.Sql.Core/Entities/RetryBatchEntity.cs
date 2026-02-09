namespace ServiceControl.Persistence.Sql.Core.Entities;

using System;
using ServiceControl.Persistence;

public class RetryBatchEntity
{
    public Guid Id { get; set; }
    public string? Context { get; set; }
    public string RetrySessionId { get; set; } = null!;
    public string? StagingId { get; set; }
    public string? Originator { get; set; }
    public string? Classifier { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? Last { get; set; }
    public string RequestId { get; set; } = null!;
    public int InitialBatchSize { get; set; }
    public RetryType RetryType { get; set; }
    public RetryBatchStatus Status { get; set; }

    // JSON column for list of retry IDs
    public string FailureRetriesJson { get; set; } = "[]";
}
