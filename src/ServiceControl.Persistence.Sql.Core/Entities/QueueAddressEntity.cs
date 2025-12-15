namespace ServiceControl.Persistence.Sql.Core.Entities;

public class QueueAddressEntity
{
    public string PhysicalAddress { get; set; } = null!;
    public int FailedMessageCount { get; set; }
}
