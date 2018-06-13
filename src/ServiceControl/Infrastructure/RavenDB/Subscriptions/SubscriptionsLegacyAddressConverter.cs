namespace ServiceControl.Infrastructure.RavenDB.Subscriptions
{
    using System.Linq;
    using Raven.Client.Listeners;
    using Raven.Json.Linq;

    class SubscriptionsLegacyAddressConverter : IDocumentConversionListener
    {
        public void BeforeConversionToDocument(string key, object entity, RavenJObject metadata)
        {
            var subscription = entity as Subscription;

            if (subscription == null)
            {
                return;
            }

            var converted = LegacyAddress.ConvertMultipleToLegacyAddress(subscription.Subscribers);
            subscription.LegacySubscriptions.Clear();
            subscription.LegacySubscriptions.AddRange(converted);
        }

        public void AfterConversionToDocument(string key, object entity, RavenJObject document, RavenJObject metadata)
        {
            
        }

        public void BeforeConversionToEntity(string key, RavenJObject document, RavenJObject metadata)
        {
        }

        public void AfterConversionToEntity(string key, RavenJObject document, RavenJObject metadata, object entity)
        {
            var subscription = entity as Subscription;

            if (subscription == null)
            {
                return;
            }

            var clients = document["Clients"];

            if (clients != null)
            {
                var converted = LegacyAddress.ParseMultipleToSubscriptionClient(subscription.LegacySubscriptions);

                var legacySubscriptions = converted.Except(subscription.Subscribers).ToArray();
                foreach (var legacySubscription in legacySubscriptions)
                {
                    subscription.Subscribers.Add(legacySubscription);
                }
            }
        }
    }
}