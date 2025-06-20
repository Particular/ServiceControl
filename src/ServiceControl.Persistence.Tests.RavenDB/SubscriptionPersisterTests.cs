namespace ServiceControl.Persistence.Tests.RavenDB
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Extensibility;
    using NServiceBus.Unicast.Subscriptions;
    using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;
    using NUnit.Framework;
    using ServiceControl.Infrastructure;
    using ServiceControl.Infrastructure.RavenDB.Subscriptions;

    [TestFixture]
    class SubscriptionPersisterTests : RavenPersistenceTestBase
    {
        [Test]
        public async Task ShouldReturnSubscriptionsForOlderVersionsOfSameMessageType()
        {
            var subscriptionPersister = new RavenSubscriptionStorage(SessionProvider, "NServiceBus.Routing.EndpointName", "TestEndpoint", [], LoggerUtil.CreateStaticLogger<RavenSubscriptionStorage>());

            var v1MessageType = new MessageType(typeof(SampleMessageType).FullName, new Version(1, 0, 0));
            var v2MessageType = new MessageType(typeof(SampleMessageType).FullName, new Version(2, 0, 0));
            var v1Subscriber = new Subscriber("V1SubscriberAddress", "V1Subscriber");

            await subscriptionPersister.Subscribe(v1Subscriber, v1MessageType, new ContextBag(), CancellationToken.None);

            var foundSubscriptions = await subscriptionPersister.GetSubscriberAddressesForMessage([v2MessageType], new ContextBag(), CancellationToken.None);

            var foundSubscriber = foundSubscriptions.Single();
            Assert.Multiple(() =>
            {
                Assert.That(foundSubscriber.Endpoint, Is.EqualTo(v1Subscriber.Endpoint));
                Assert.That(foundSubscriber.TransportAddress, Is.EqualTo(v1Subscriber.TransportAddress));
            });
        }

        [Test]
        public async Task ShouldReturnSubscriptionsForNewerVersionsOfSameMessageType()
        {
            var subscriptionPersister = new RavenSubscriptionStorage(SessionProvider, "NServiceBus.Routing.EndpointName", "TestEndpoint", [], LoggerUtil.CreateStaticLogger<RavenSubscriptionStorage>());

            var v1MessageType = new MessageType(typeof(SampleMessageType).FullName, new Version(1, 0, 0));
            var v2MessageType = new MessageType(typeof(SampleMessageType).FullName, new Version(2, 0, 0));
            var v2Subscriber = new Subscriber("V2SubscriberAddress", "V2Subscriber");

            await subscriptionPersister.Subscribe(v2Subscriber, v2MessageType, new ContextBag(), CancellationToken.None);

            var foundSubscriptions = await subscriptionPersister.GetSubscriberAddressesForMessage([v1MessageType], new ContextBag(), CancellationToken.None);

            var foundSubscriber = foundSubscriptions.Single();
            Assert.Multiple(() =>
            {
                Assert.That(foundSubscriber.Endpoint, Is.EqualTo(v2Subscriber.Endpoint));
                Assert.That(foundSubscriber.TransportAddress, Is.EqualTo(v2Subscriber.TransportAddress));
            });
        }
    }

    public class SampleMessageType
    {
    }
}