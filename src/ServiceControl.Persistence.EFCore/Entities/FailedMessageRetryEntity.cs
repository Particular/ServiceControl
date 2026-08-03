namespace ServiceControl.Persistence.EFCore.Entities;

public class FailedMessageRetryEntity
{
    public Guid UniqueMessageId { get; set; }

    public Guid RetryBatchId { get; set; }

    public int StageAttempts { get; set; }
}
