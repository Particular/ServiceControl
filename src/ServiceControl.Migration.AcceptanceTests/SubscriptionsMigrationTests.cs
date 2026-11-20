namespace ServiceControl.Migration.AcceptanceTests;

using System;
using System.Linq;
using System.Threading.Tasks;
using NServiceBus.Extensibility;
using NServiceBus.Unicast.Subscriptions;
using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;
using NUnit.Framework;
using ServiceControl.Persistence.DataMigration;

[TestFixture]
[NonParallelizable]
class SubscriptionsMigrationTests : MigrationAcceptanceTest
{
    [Test]
    public async Task Two_message_type_versions_for_the_same_subscriber_merge_onto_one_row_and_the_merge_is_already_present()
    {
        await SeedSourceSubscriptions(
            new SubscriptionRow("MyMessages.OrderPlaced", new Version(1, 0, 0), "Billing@BILLING01", "Billing"),
            new SubscriptionRow("MyMessages.OrderPlaced", new Version(2, 0, 0), "Billing@BILLING01", "Billing"));

        var result = await CopyCategory("Subscriptions");

        Assert.That(result.Copied, Is.EqualTo(1));
        Assert.That(result.AlreadyPresent, Is.EqualTo(1));
        Assert.That(result.Skipped, Is.Zero, "a version merge loses no row the target lacks, and a skip would leave the required category Failed");
        Assert.That(result.Saved.State, Is.EqualTo(MigrationCategoryState.Complete), "a merge is not a skip, so the category is Done");

        var subscribers = await SubscriptionStorage.GetSubscriberAddressesForMessage(
            [new MessageType("MyMessages.OrderPlaced", new Version(1, 0, 0))], new ContextBag(), TestContext.CurrentContext.CancellationToken);

        Assert.That(subscribers.Count(), Is.EqualTo(1));
        Assert.That(subscribers.Select(subscriber => (subscriber.TransportAddress, subscriber.Endpoint)), Is.EqualTo(new[] { ("Billing@BILLING01", "Billing") }),
            "the copied row holds the subscriber's address and endpoint as the source has them");
    }

    [Test]
    public async Task A_legacy_subscriber_with_no_endpoint_gets_the_endpoint_the_product_stores_for_one()
    {
        var copiedType = new MessageType("MyMessages.OrderPlaced", new Version(1, 0, 0));
        var storedType = new MessageType("MyMessages.OrderShipped", new Version(1, 0, 0));
        var token = TestContext.CurrentContext.CancellationToken;

        await SeedSourceSubscriptions(new SubscriptionRow(copiedType.TypeName, copiedType.Version, "Billing@BILLING01", Endpoint: null));

        var result = await CopyCategory("Subscriptions");

        // Another type, so the product's upsert cannot overwrite the copied row it is compared with.
        await SubscriptionStorage.Subscribe(new Subscriber("Billing@BILLING01", null), storedType, new ContextBag(), token);

        var copied = await SubscriptionStorage.GetSubscriberAddressesForMessage([copiedType], new ContextBag(), token);
        var stored = await SubscriptionStorage.GetSubscriberAddressesForMessage([storedType], new ContextBag(), token);

        Assert.That(result.Copied, Is.EqualTo(1), "Endpoint is NOT NULL on the target, so a row built without a fallback fails the batch and leaves the required category Failed");
        Assert.That(result.Saved.State, Is.EqualTo(MigrationCategoryState.Complete));
        Assert.That(copied.Single().TransportAddress, Is.EqualTo("Billing@BILLING01"));
        Assert.That(copied.Single().Endpoint, Is.EqualTo(stored.Single().Endpoint),
            "the copy must store the endpoint SubscriptionStorage itself stores for the same legacy subscriber");
        Assert.That(copied.Single().Endpoint, Is.EqualTo("Billing"), "the endpoint is the part of the address before the '@'");
    }
}
