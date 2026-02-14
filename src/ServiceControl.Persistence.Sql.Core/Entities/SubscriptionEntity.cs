namespace ServiceControl.Persistence.Sql.Core.Entities;

public class SubscriptionEntity
{
    public string Id { get; set; } = null!;
    public string MessageTypeTypeName { get; set; } = null!;
    public int MessageTypeVersion { get; set; }
    public string SubscribersJson { get; set; } = null!;
}
