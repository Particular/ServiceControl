namespace ServiceControl.Infrastructure.RavenDB.Subscriptions
{
    using System.Linq;
    using Newtonsoft.Json.Linq;
    using Raven.Client.Listeners;

    class SubscriptionsLegacyAddressConverter : IDocumentConversionListener
    {
        public void BeforeConversionToDocument(string key, object entity, JObject metadata)
        {
            if (!(entity is Subscription subscription))
            {
                return;
            }

            var converted = LegacyAddress.ConvertMultipleToLegacyAddress(subscription.Subscribers);
            subscription.LegacySubscriptions.Clear();
            subscription.LegacySubscriptions.AddRange(converted);
        }

        public void AfterConversionToDocument(string key, object entity, JObject document, JObject metadata)
        {
        }

        public void BeforeConversionToEntity(string key, JObject document, JObject metadata)
        {
        }

        public void AfterConversionToEntity(string key, JObject document, JObject metadata, object entity)
        {
            if (!(entity is Subscription subscription))
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