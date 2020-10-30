namespace ServiceControl.UnitTests.Infrastructure.RavenDB
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NServiceBus.Unicast.Subscriptions;
    using NUnit.Framework;
    using Raven.Client;
    using Raven.Client.Documents;
    using Raven.TestDriver;
    using ServiceControl.Infrastructure.RavenDB.Subscriptions;

    [TestFixture]
    class LegacyDocumentConversionTests : RavenTestDriver
    {
        protected override void PreInitialize(IDocumentStore documentStore)
        {
            base.PreInitialize(documentStore);
            LegacyDocumentConversion.Install(documentStore);
        }

        [TestCase("NServiceBus.Subscription, NServiceBus.Core")]
        [TestCase("NServiceBus.Subscription, NServiceBus.RavenDB")]
        public async Task CanLoadSubscriptionsWithLegacyClrTypes(string legacyClrType)
        {
            var store = GetDocumentStore();
            var legacySubscription = CreateLegacySubscription();

            // Save the subscription and change the CLR Type
            using (var session = store.OpenAsyncSession())
            {
                await session.StoreAsync(legacySubscription);
                session.Advanced.GetMetadataFor(legacySubscription)[Constants.Documents.Metadata.RavenClrType] = legacyClrType;
                await session.SaveChangesAsync();
            }

            // Now load the subscription and it should be loaded as the correct type
            using (var session = store.OpenAsyncSession())
            {
                var loadedSubscription = await session.LoadAsync<object>(legacySubscription.Id);
                Assert.That(loadedSubscription, Is.Not.Null, "Subscription should be loaded");
                Assert.IsTrue(loadedSubscription is Subscription, "Incorrect CLR Type should be converted to correct type");
            }

        }

        private static Subscription CreateLegacySubscription()
        {
            return new Subscription
            {
                Id = "SubscriptionId",
                MessageType = new MessageType("EventType", "1.0"),
                Subscribers = new List<SubscriptionClient>
                {
                    new SubscriptionClient
                    {
                        Endpoint = "SubscriberName",
                        TransportAddress = "subscriber@MACHINE"
                    }
                }
            };
        }
    }
}
