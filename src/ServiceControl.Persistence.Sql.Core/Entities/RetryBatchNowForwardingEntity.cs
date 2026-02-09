namespace ServiceControl.Persistence.Sql.Core.Entities;

public class RetryBatchNowForwardingEntity
{
    public int Id { get; set; }
    public string RetryBatchId { get; set; } = null!;

    // This is a singleton entity - only one forwarding batch at a time
    public const int SingletonId = 1;
}
