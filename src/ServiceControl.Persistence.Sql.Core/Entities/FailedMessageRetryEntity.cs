namespace ServiceControl.Persistence.Sql.Core.Entities;

using System;

public class FailedMessageRetryEntity
{
    public Guid Id { get; set; }
    public string FailedMessageId { get; set; } = null!;
    public string? RetryBatchId { get; set; }
    public int StageAttempts { get; set; }
}
