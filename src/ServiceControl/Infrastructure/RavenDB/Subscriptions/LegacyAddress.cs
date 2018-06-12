namespace ServiceControl.Infrastructure.RavenDB.Subscriptions
{
    using System;
    using System.Collections.Generic;
    using Raven.Imports.Newtonsoft.Json.Linq;
    using Raven.Json.Linq;

    class LegacyAddress
    {
        public string Queue { get; set; }
        public string Machine { get; set; }
        public static List<SubscriptionClient> ParseMultipleToSubscriptionClient(List<LegacyAddress> addresses) => addresses.ConvertAll(ParseToSubscriptionClient);
        public static List<LegacyAddress> ConvertMultipleToLegacyAddress(List<SubscriptionClient> subscriptions) => subscriptions.ConvertAll(ConvertToLegacyAddress);

        public static SubscriptionClient ParseToSubscriptionClient(LegacyAddress address)
        {
            var queue = address.Queue;
            var machine = address.Machine;

            // Previously known as IgnoreMachineName (for brokers)
            if (string.IsNullOrEmpty(machine))
            {
                return new SubscriptionClient
                {
                    TransportAddress = queue,
                    Endpoint = null
                };
            }

            return new SubscriptionClient
            {
                TransportAddress = queue + "@" + machine,
                Endpoint = null
            };
        }

        public static LegacyAddress ConvertToLegacyAddress(SubscriptionClient client)
        {
            var atIndex = client.TransportAddress?.IndexOf("@", StringComparison.InvariantCulture);

            if (atIndex.HasValue && atIndex.Value > -1)
            {
                return new LegacyAddress
                {
                    Queue = client.TransportAddress.Substring(0, atIndex.Value),
                    Machine = client.TransportAddress.Substring(atIndex.Value + 1)
                };
            }

            return new LegacyAddress
            {
                Queue = client.TransportAddress,
                Machine = null
            };
        }

        public static string ParseToString(Func<RavenJToken> tokenSelector)
        {
            var token = tokenSelector();

            // When we have the new timeout data we just return the value
            if (token.Type == JTokenType.String)
            {
                return token.Value<string>();
            }

            var queue = token.Value<string>("Queue");
            var machine = token.Value<string>("Machine");

            // Previously known as IgnoreMachineName (for brokers)
            if (string.IsNullOrEmpty(machine))
            {
                return queue;
            }

            return queue + "@" + machine;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return obj is LegacyAddress && Equals((LegacyAddress) obj);
        }

        bool Equals(LegacyAddress obj) => string.Equals(Queue, obj.Queue) && string.Equals(Machine, obj.Machine);

        public override int GetHashCode() => Queue.GetHashCode() ^ Machine.GetHashCode();
    }
}