namespace ServiceControl.Persistence.EFCore.Entities;

public class ExternalIntegrationDispatchRequestEntity
{
    // Auto-increment, so ordering by Id gives natural FIFO dispatch order.
    public long Id { get; set; }

    public required string DispatchContextTypeName { get; set; }

    public required string DispatchContextJson { get; set; }
}
