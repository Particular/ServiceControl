namespace ServiceControl.Infrastructure.RavenDB.Subscriptions
{
    using System.Collections.Generic;
    using NServiceBus;
    using NServiceBus.Unicast.Subscriptions;
    using Raven.Imports.Newtonsoft.Json;

    class Subscription
    {
        public string Id { get; set; }

        [JsonConverter(typeof(MessageTypeConverter))]
        public MessageType MessageType { get; set; }

        public List<Address> Clients { get; set; }
    }
}