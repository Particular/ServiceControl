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

        public List<SubscriptionClient> Subscribers { get; set; } = [];
    }
}