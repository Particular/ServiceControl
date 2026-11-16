#nullable enable

namespace ServiceControl.Persistence.RavenDB.DataMigration.Readers;

using System.Collections.Generic;
using ServiceControl.Infrastructure.RavenDB.Subscriptions;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Reads the subscriptions document as one row per subscriber of each message type and version.
/// </summary>
sealed class SubscriptionsReader(RavenReadOnlySourceLifecycle lifecycle)
    : SingleDocumentReader<Subscriptions, SubscriptionRow>(lifecycle, lifecycle.Settings.DatabaseName, Subscriptions.SingleDocumentId)
{
    public override string CategoryId => MigrationCategoryIds.Subscriptions;

    protected override IEnumerable<(string SourceId, SubscriptionRow Document)> RowsFrom(Subscriptions document)
    {
        foreach (var subscription in document.All.Values)
        {
            foreach (var subscriber in subscription.Subscribers)
            {
                yield return ($"{subscription.Id}|{subscriber.TransportAddress}", new SubscriptionRow(subscription.MessageType.TypeName, subscription.MessageType.Version, subscriber.TransportAddress, subscriber.Endpoint));
            }
        }
    }
}
