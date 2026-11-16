namespace ServiceControl.Persistence.EFCore.DataMigration;

using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.EFCore.Entities;

/// <summary>
/// Turns a subscription from the source into the row the target stores.
/// The version is dropped, because the row has no column for it.
/// </summary>
static class SubscriptionRowMapper
{
    public static SubscriptionEntity Map(SubscriptionRow row) => new()
    {
        MessageType = row.MessageType,
        TransportAddress = row.TransportAddress,
        // The value SubscriptionStorage.Subscribe stores for a subscriber with no endpoint.
        Endpoint = row.Endpoint ?? row.TransportAddress.Split('@').First()
    };
}
