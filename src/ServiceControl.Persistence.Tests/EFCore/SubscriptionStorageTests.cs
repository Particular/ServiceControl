namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus.Extensibility;
using NServiceBus.Unicast.Subscriptions;
using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;
using NUnit.Framework;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Implementation;

class SubscriptionStorageTests : PersistenceTestBase
{
    const string LocalEndpointName = "Particular.ServiceControl";
    const string LocalAddress = "Particular.ServiceControl@local";

    static readonly MessageType SampleEventV1 = new(typeof(SampleEvent).FullName, new Version(1, 0, 0));
    static readonly MessageType SampleEventV2 = new(typeof(SampleEvent).FullName, new Version(2, 0, 0));
    static readonly MessageType OtherEvent = new(typeof(AnotherSampleEvent).FullName, new Version(1, 0, 0));

    [Test]
    public async Task Subscribe_stores_the_subscriber()
    {
        var storage = CreateStorage();

        await Subscribe(storage, new Subscriber("SalesAddress", "Sales"), SampleEventV1);

        var subscribers = await GetSubscribers(storage, SampleEventV1);

        Assert.That(subscribers, Has.Count.EqualTo(1));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(subscribers[0].TransportAddress, Is.EqualTo("SalesAddress"));
            Assert.That(subscribers[0].Endpoint, Is.EqualTo("Sales"));
        }
    }

    [Test]
    public async Task Subscribing_twice_stores_a_single_row()
    {
        var storage = CreateStorage();
        var subscriber = new Subscriber("SalesAddress", "Sales");

        await Subscribe(storage, subscriber, SampleEventV1);
        await Subscribe(storage, subscriber, SampleEventV1);

        Assert.That(await GetStoredSubscriptions(), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Resubscribing_updates_the_endpoint_name()
    {
        var storage = CreateStorage();

        await Subscribe(storage, new Subscriber("SalesAddress", "Sales"), SampleEventV1);
        await Subscribe(storage, new Subscriber("SalesAddress", "Sales.Renamed"), SampleEventV1);

        var stored = await GetStoredSubscriptions();

        Assert.That(stored, Has.Count.EqualTo(1));
        Assert.That(stored[0].Endpoint, Is.EqualTo("Sales.Renamed"));
    }

    [Test]
    public async Task Unsubscribe_removes_only_the_matching_subscription()
    {
        var storage = CreateStorage();
        var sales = new Subscriber("SalesAddress", "Sales");
        var shipping = new Subscriber("ShippingAddress", "Shipping");

        await Subscribe(storage, sales, SampleEventV1);
        await Subscribe(storage, shipping, SampleEventV1);
        await Subscribe(storage, sales, OtherEvent);

        await storage.Unsubscribe(sales, SampleEventV1, new ContextBag(), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((await GetSubscribers(storage, SampleEventV1)).Select(s => s.TransportAddress), Is.EqualTo(new[] { "ShippingAddress" }));
            Assert.That((await GetSubscribers(storage, OtherEvent)).Select(s => s.TransportAddress), Is.EqualTo(new[] { "SalesAddress" }));
        }
    }

    [Test]
    public async Task Unsubscribing_something_that_was_never_subscribed_is_a_no_op()
    {
        var storage = CreateStorage();

        await storage.Unsubscribe(new Subscriber("SalesAddress", "Sales"), SampleEventV1, new ContextBag(), CancellationToken.None);

        Assert.That(await GetStoredSubscriptions(), Is.Empty);
    }

    [Test]
    public async Task Should_return_subscriptions_for_other_versions_of_the_same_message_type()
    {
        var storage = CreateStorage();

        await Subscribe(storage, new Subscriber("V1SubscriberAddress", "V1Subscriber"), SampleEventV1);

        var subscribers = await GetSubscribers(storage, SampleEventV2);

        Assert.That(subscribers, Has.Count.EqualTo(1));
        Assert.That(subscribers[0].TransportAddress, Is.EqualTo("V1SubscriberAddress"));
    }

    [Test]
    public async Task Subscriber_is_returned_once_when_subscribed_to_several_types_in_the_hierarchy()
    {
        var storage = CreateStorage();
        var sales = new Subscriber("SalesAddress", "Sales");

        await Subscribe(storage, sales, SampleEventV1);
        await Subscribe(storage, sales, OtherEvent);

        var subscribers = await GetSubscribers(storage, SampleEventV1, OtherEvent);

        Assert.That(subscribers, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Endpoint_is_derived_from_the_transport_address_when_the_subscriber_does_not_supply_one()
    {
        var storage = CreateStorage();

        await Subscribe(storage, new Subscriber("Sales@MachineName", null), SampleEventV1);

        var subscribers = await GetSubscribers(storage, SampleEventV1);

        Assert.That(subscribers[0].Endpoint, Is.EqualTo("Sales"));
    }

    [Test]
    public async Task Subscriptions_from_the_local_endpoint_are_ignored()
    {
        var storage = CreateStorage();

        await Subscribe(storage, new Subscriber(LocalAddress, LocalEndpointName), SampleEventV1);

        Assert.That(await GetStoredSubscriptions(), Is.Empty);
    }

    [Test]
    public async Task Locally_handled_event_types_resolve_to_the_local_address()
    {
        var storage = CreateStorage(locallyHandledEventTypes: [SampleEventV1]);

        var subscribers = await GetSubscribers(storage, SampleEventV2);

        Assert.That(subscribers, Has.Count.EqualTo(1));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(subscribers[0].TransportAddress, Is.EqualTo(LocalAddress));
            Assert.That(subscribers[0].Endpoint, Is.EqualTo(LocalEndpointName));
        }
    }

    [Test]
    public async Task Locally_handled_event_types_do_not_leak_into_other_message_types()
    {
        var storage = CreateStorage(locallyHandledEventTypes: [SampleEventV1]);

        Assert.That(await GetSubscribers(storage, OtherEvent), Is.Empty);
    }

    [Test]
    public async Task Subscriber_lookups_are_served_from_the_cache_until_it_expires()
    {
        var storage = CreateStorage(cacheDuration: TimeSpan.FromSeconds(60));

        Assert.That(await GetSubscribers(storage, SampleEventV1), Is.Empty);

        await StoreSubscriptionDirectly(SampleEventV1.TypeName, "SalesAddress", "Sales");

        Assert.That(await GetSubscribers(storage, SampleEventV1), Is.Empty, "the cached, empty result should still be served");

        PersistenceTestsContext.FakeTime.Advance(TimeSpan.FromSeconds(60));

        Assert.That(await GetSubscribers(storage, SampleEventV1), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Subscribing_evicts_the_cached_lookup_for_that_message_type()
    {
        var storage = CreateStorage(cacheDuration: TimeSpan.FromSeconds(60));

        Assert.That(await GetSubscribers(storage, SampleEventV1), Is.Empty);
        Assert.That(await GetSubscribers(storage, OtherEvent), Is.Empty);

        await Subscribe(storage, new Subscriber("SalesAddress", "Sales"), SampleEventV1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await GetSubscribers(storage, SampleEventV1), Has.Count.EqualTo(1));
            Assert.That(await GetSubscribers(storage, OtherEvent), Is.Empty);
        }
    }

    [Test]
    public async Task Unsubscribing_evicts_the_cached_lookup_for_that_message_type()
    {
        var storage = CreateStorage(cacheDuration: TimeSpan.FromSeconds(60));
        var sales = new Subscriber("SalesAddress", "Sales");

        await Subscribe(storage, sales, SampleEventV1);
        Assert.That(await GetSubscribers(storage, SampleEventV1), Has.Count.EqualTo(1));

        await storage.Unsubscribe(sales, SampleEventV1, new ContextBag(), CancellationToken.None);

        Assert.That(await GetSubscribers(storage, SampleEventV1), Is.Empty);
    }

    SubscriptionStorage CreateStorage(TimeSpan cacheDuration = default, MessageType[] locallyHandledEventTypes = null) =>
        new(ServiceProvider.GetRequiredService<IServiceScopeFactory>(),
            LocalEndpointName,
            LocalAddress,
            locallyHandledEventTypes ?? [],
            cacheDuration,
            PersistenceTestsContext.FakeTime);

    static Task Subscribe(SubscriptionStorage storage, Subscriber subscriber, MessageType messageType) =>
        storage.Subscribe(subscriber, messageType, new ContextBag(), CancellationToken.None);

    static async Task<IList<Subscriber>> GetSubscribers(SubscriptionStorage storage, params MessageType[] messageTypes)
    {
        var subscribers = await storage.GetSubscriberAddressesForMessage(messageTypes, new ContextBag(), CancellationToken.None);

        return subscribers.ToList();
    }

    async Task<IList<SubscriptionEntity>> GetStoredSubscriptions()
    {
        await using var scope = ServiceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

        return await dbContext.Subscriptions.AsNoTracking().ToListAsync();
    }

    async Task StoreSubscriptionDirectly(string messageType, string transportAddress, string endpoint)
    {
        await using var scope = ServiceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

        dbContext.Subscriptions.Add(new SubscriptionEntity { MessageType = messageType, TransportAddress = transportAddress, Endpoint = endpoint });

        await dbContext.SaveChangesAsync();
    }

    class SampleEvent;

    class AnotherSampleEvent;
}
