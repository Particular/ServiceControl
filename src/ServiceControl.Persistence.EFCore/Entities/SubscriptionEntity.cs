namespace ServiceControl.Persistence.EFCore.Entities;

public class SubscriptionEntity
{
    public required string MessageType { get; set; }
    public required string TransportAddress { get; set; }
    public required string Endpoint { get; set; }
}
