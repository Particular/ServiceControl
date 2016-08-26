namespace ServiceControl.Infrastructure.RavenDB.Subscriptions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using NServiceBus;
    using NServiceBus.Unicast.Subscriptions;
    using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;
    using Raven.Client;

    internal class SubscriptionPersister : ISubscriptionStorage, IDisposable
    {
        private readonly IDocumentStore store;
        private readonly MD5CryptoServiceProvider provider = new MD5CryptoServiceProvider();

        public SubscriptionPersister(IDocumentStore store)
        {
            this.store = store;
        }

        public void Dispose()
        {
            provider.Dispose();
        }

        public void Init()
        {
        }

        public void Subscribe(Address client, IEnumerable<MessageType> messageTypes)
        {
            var messageTypeLookup = messageTypes.ToDictionary(FormatId);

            using (var session = OpenSession())
            {
                session.Advanced.UseOptimisticConcurrency = true;

                var existingSubscriptions = GetSubscriptions(messageTypeLookup.Values, session).ToLookup(m => m.Id);

                var newAndExistingSubscriptions = messageTypeLookup
                    .Select(id => existingSubscriptions[id.Key].SingleOrDefault() ?? StoreNewSubscription(session, id.Key, id.Value))
                    .Where(subscription => subscription.Clients.All(c => c != client)).ToArray();

                foreach (var subscription in newAndExistingSubscriptions)
                {
                    subscription.Clients.Add(client);
                }

                session.SaveChanges();
            }
        }

        public void Unsubscribe(Address client, IEnumerable<MessageType> messageTypes)
        {
            using (var session = OpenSession())
            {
                session.Advanced.UseOptimisticConcurrency = true;

                var subscriptions = GetSubscriptions(messageTypes, session);

                foreach (var subscription in subscriptions)
                {
                    subscription.Clients.Remove(client);
                }

                session.SaveChanges();
            }
        }

        public IEnumerable<Address> GetSubscriberAddressesForMessage(IEnumerable<MessageType> messageTypes)
        {
            using (var session = OpenSession())
            {
                using (session.Advanced.DocumentStore.AggressivelyCache())
                {
                    return GetSubscriptions(messageTypes, session)
                        .SelectMany(s => s.Clients)
                        .Distinct();
                }
            }
        }

        private IDocumentSession OpenSession()
        {
            var session = store.OpenSession();
            session.Advanced.AllowNonAuthoritativeInformation = false;
            return session;
        }

        private string FormatId(MessageType messageType)
        {
            // use MD5 hash to get a 16-byte hash of the string
            var inputBytes = Encoding.Default.GetBytes($"{messageType.TypeName}/{messageType.Version.Major}");
            var hashBytes = provider.ComputeHash(inputBytes);
            // generate a guid from the hash:
            var id = new Guid(hashBytes);

            return $"Subscriptions/{id}";
        }

        private IEnumerable<Subscription> GetSubscriptions(IEnumerable<MessageType> messageTypes, IDocumentSession session)
        {
            var ids = messageTypes
                .Select(FormatId);

            return session.Load<Subscription>(ids).Where(s => s != null);
        }

        private static Subscription StoreNewSubscription(IDocumentSession session, string id, MessageType messageType)
        {
            var subscription = new Subscription
            {
                Clients = new List<Address>(),
                Id = id,
                MessageType = messageType
            };
            session.Store(subscription);

            return subscription;
        }
    }
}