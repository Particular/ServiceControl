namespace ServiceControl.Infrastructure.RavenDB.Subscriptions
{
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using NServiceBus.Unicast.Subscriptions;

    class Subscription
    {
        public string Id { get; set; }

        [JsonConverter(typeof(MessageTypeConverter))]
        public MessageType MessageType { get; set; }

        public List<SubscriptionClient> Subscribers
        {
            get
            {
                if (subscribers == null)
                {
                    subscribers = new List<SubscriptionClient>();
                }

                return subscribers;
            }
            set => subscribers = value;
        }

        [JsonProperty("Clients")]
        public List<LegacyAddress> LegacySubscriptions
        {
            get
            {
                if (legacySubscriptions == null)
                {
                    legacySubscriptions = new List<LegacyAddress>();
                }

                return legacySubscriptions;
            }
        }

        List<SubscriptionClient> subscribers;
        List<LegacyAddress> legacySubscriptions;
    }
}