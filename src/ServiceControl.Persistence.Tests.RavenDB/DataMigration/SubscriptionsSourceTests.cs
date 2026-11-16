namespace ServiceControl.Persistence.Tests.RavenDB.DataMigration;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NServiceBus.Unicast.Subscriptions;
using NUnit.Framework;
using ServiceControl.Infrastructure.RavenDB.Subscriptions;
using ServiceControl.Persistence.DataMigration;

class SubscriptionsSourceTests : RavenMigrationSourceTestBase
{
    [Test]
    public async Task Flattens_the_dictionary_of_subscriptions_into_one_row_per_subscriber()
    {
        var eventV1 = new MessageType("MyMessages.OrderPlaced", new Version(1, 0, 0));
        var eventV2 = new MessageType("MyMessages.OrderPlaced", new Version(2, 0, 0));

        var document = new Subscriptions();
        document.All["v1"] = new Subscription
        {
            Id = "v1",
            MessageType = eventV1,
            Subscribers = [
                new SubscriptionClient { Endpoint = "Billing", TransportAddress = "Billing@BILLING01" },
                new SubscriptionClient { Endpoint = "Shipping", TransportAddress = "Shipping@SHIP01" }
            ]
        };
        document.All["v2"] = new Subscription
        {
            Id = "v2",
            MessageType = eventV2,
            Subscribers = [new SubscriptionClient { Endpoint = "Billing", TransportAddress = "Billing@BILLING01" }]
        };

        using (var session = await SessionProvider.OpenSession())
        {
            await session.StoreAsync(document, Subscriptions.SingleDocumentId);
            await session.SaveChangesAsync();
        }

        var batches = await Read();

        Assert.That(batches.Last().Cursor, Is.EqualTo(Subscriptions.SingleDocumentId));
        Assert.That(batches.SelectMany(batch => batch.Rows).Select(row => (row.SourceId, (SubscriptionRow)row.Document)), Is.EquivalentTo(new[]
        {
            ("v1|Billing@BILLING01", new SubscriptionRow("MyMessages.OrderPlaced", new Version(1, 0, 0), "Billing@BILLING01", "Billing")),
            ("v1|Shipping@SHIP01", new SubscriptionRow("MyMessages.OrderPlaced", new Version(1, 0, 0), "Shipping@SHIP01", "Shipping")),
            ("v2|Billing@BILLING01", new SubscriptionRow("MyMessages.OrderPlaced", new Version(2, 0, 0), "Billing@BILLING01", "Billing"))
        }), "each row carries every value its subscriber holds, under an id naming the subscription and the address");
    }

    async Task<List<MigrationBatch>> Read()
    {
        await using var source = await OpenMigrationSource();
        return await CollectBatches(source, MigrationCategoryRegistry.Find(MigrationCategoryIds.Subscriptions));
    }
}
