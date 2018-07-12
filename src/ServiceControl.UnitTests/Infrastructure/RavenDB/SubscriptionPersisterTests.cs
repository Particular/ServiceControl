namespace ServiceControl.UnitTests.Infrastructure.RavenDB
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus.Extensibility;
    using NServiceBus.Settings;
    using NServiceBus.Unicast.Subscriptions;
    using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;
    using NUnit.Framework;
    using Raven.Client;
    using ServiceControl.Infrastructure.RavenDB.Subscriptions;

    [TestFixture]
    public class SubscriptionPersisterTests
    {
        [Test]
        public async Task ShouldReturnSubscriptionsForOlderVersionsOfSameMessageType()
        {
            var settings = new SettingsHolder();
            var subscriptionPersister = new SubscriptionPersister(documentStore, settings, "NServiceBus.Routing.EndpointName", "TestEndpoint", new MessageType[0]);

            var v1MessageType = new MessageType(typeof(SampleMessageType).FullName, new Version(1, 0, 0));
            var v2MessageType = new MessageType(typeof(SampleMessageType).FullName, new Version(2, 0, 0));
            var v1Subscriber = new Subscriber("V1SubscriberAddress", "V1Subscriber");

            await subscriptionPersister.Subscribe(v1Subscriber, v1MessageType, new ContextBag());

            var foundSubscriptions = await subscriptionPersister.GetSubscriberAddressesForMessage(new[] {v2MessageType}, new ContextBag());

            var foundSubscriber = foundSubscriptions.Single();
            Assert.AreEqual(v1Subscriber.Endpoint, foundSubscriber.Endpoint);
            Assert.AreEqual(v1Subscriber.TransportAddress, foundSubscriber.TransportAddress);
        }

        [Test]
        public async Task ShouldReturnSubscriptionsForNewerVersionsOfSameMessageType()
        {
            var settings = new SettingsHolder();
            var subscriptionPersister = new SubscriptionPersister(documentStore, settings, "NServiceBus.Routing.EndpointName", "TestEndpoint", new MessageType[0]);

            var v1MessageType = new MessageType(typeof(SampleMessageType).FullName, new Version(1, 0, 0));
            var v2MessageType = new MessageType(typeof(SampleMessageType).FullName, new Version(2, 0, 0));
            var v2Subscriber = new Subscriber("V2SubscriberAddress", "V2Subscriber");

            await subscriptionPersister.Subscribe(v2Subscriber, v2MessageType, new ContextBag());

            var foundSubscriptions = await subscriptionPersister.GetSubscriberAddressesForMessage(new[] { v1MessageType }, new ContextBag());

            var foundSubscriber = foundSubscriptions.Single();
            Assert.AreEqual(v2Subscriber.Endpoint, foundSubscriber.Endpoint);
            Assert.AreEqual(v2Subscriber.TransportAddress, foundSubscriber.TransportAddress);
        }

        [SetUp]
        public void SetUp()
        {
            documentStore = InMemoryStoreBuilder.GetInMemoryStore();
        }

        [TearDown]
        public void TearDown()
        {
            documentStore.Dispose();
        }

        IDocumentStore documentStore;
    }

    public class SampleMessageType { }
}