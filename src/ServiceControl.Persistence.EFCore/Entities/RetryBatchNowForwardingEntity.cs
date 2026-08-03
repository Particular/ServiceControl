namespace ServiceControl.Persistence.EFCore.Entities;

public class RetryBatchNowForwardingEntity
{
    public const int SingleRowId = 1;

    public int Id { get; set; } = SingleRowId;

    public Guid RetryBatchId { get; set; }
}
