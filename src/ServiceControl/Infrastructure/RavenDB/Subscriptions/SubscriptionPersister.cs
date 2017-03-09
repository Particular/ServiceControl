namespace ServiceControl.Infrastructure.RavenDB.Subscriptions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Settings;
    using NServiceBus.Unicast.Subscriptions;
    using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;
    using Raven.Client;

    internal class SubscriptionPersister : ISubscriptionStorage, IPrimableSubscriptionStorage
    {
        private IDocumentStore store;
        private Address localAddress;
        private Subscriptions subscriptions;
        private ILookup<MessageType, Address> subscriptionsLookup;
        private MessageType[] locallyHandledEventTypes;

        private object subscriptionsLock = new object();

        public SubscriptionPersister(IDocumentStore store, ReadOnlySettings settings)
        {
            this.store = store;
            localAddress = settings.LocalAddress();

            locallyHandledEventTypes = settings.GetAvailableTypes().Implementing<IEvent>().Select(e => new MessageType(e)).ToArray();


            SetSubscriptions(new Subscriptions());
        }

        public void Init()
        {
        }

        public void Subscribe(Address client, IEnumerable<MessageType> messageTypes)
        {
            if (client == localAddress)
            {
                return;
            }

            lock (subscriptionsLock)
            {
                var needsSave = false;

                foreach (var messageType in messageTypes)
                {
                    if (AddOrUpdateSubscription(messageType, client))
                    {
                        needsSave = true;
                    }
                }

                if (needsSave)
                {
                    SaveSubscriptions();
                }
            }
        }

        private bool AddOrUpdateSubscription(MessageType messageType, Address client)
        {
            var key = FormatId(messageType);

            Subscription subscription;
            if (subscriptions.All.TryGetValue(key, out subscription))
            {
                if (subscription.Clients.Contains(client))
                {
                    return false;
                }
                subscription.Clients.Add(client);
                return true;
            }

            // New Subscription
            subscription = new Subscription
            {
                Id = key,
                Clients = new List<Address>
                {
                    client
                },
                MessageType = messageType
            };
            subscriptions.All.Add(key, subscription);
            return true;
        }

        public void Unsubscribe(Address client, IEnumerable<MessageType> messageTypes)
        {
            lock (subscriptionsLock)
            {
                var needsSave = false;

                foreach (var messageType in messageTypes)
                {
                    Subscription subscription;
                    if (subscriptions.All.TryGetValue(FormatId(messageType), out subscription))
                    {
                        if (subscription.Clients.Remove(client))
                        {
                            needsSave = true;
                        }
                    }
                }

                if (needsSave)
                {
                    SaveSubscriptions();
                }
            }
        }

        private void SaveSubscriptions()
        {
            using (var session = store.OpenSession())
            {
                session.Store(subscriptions, Subscriptions.SingleDocumentId);
                UpdateLookup();
                session.SaveChanges();
            }
        }

        private void UpdateLookup()
        {
            subscriptionsLookup = (from subscription in subscriptions.All.Values
                                   from client in subscription.Clients
                                   select new
                                   {
                                       subscription.MessageType,
                                       Address = client
                                   }).Union(from eventType in locallyHandledEventTypes
                                            select new
                                            {
                                                MessageType = eventType,
                                                Address = localAddress
                                            }
                                    ).ToLookup(x => x.MessageType, x => x.Address);
        }

        public IEnumerable<Address> GetSubscriberAddressesForMessage(IEnumerable<MessageType> messageTypes)
            => messageTypes.SelectMany(x => subscriptionsLookup[x]).Distinct();

        private string FormatId(MessageType messageType)
        {
            // use MD5 hash to get a 16-byte hash of the string
            var inputBytes = Encoding.Default.GetBytes($"{messageType.TypeName}/{messageType.Version.Major}");
            using (var provider = new MD5CryptoServiceProvider())
            {
                var hashBytes = provider.ComputeHash(inputBytes);

                // generate a guid from the hash:
                var id = new Guid(hashBytes);
                return $"Subscriptions/{id}";
            }
        }

        private void SetSubscriptions(Subscriptions newSubscriptions)
        {
            lock (subscriptionsLock)
            {
                subscriptions = newSubscriptions;
                UpdateLookup();
            }
        }

        public void Prime()
        {
            using (var session = store.OpenSession())
            {
                var primeSubscriptions = LoadSubscriptions(session) ?? MigrateSubscriptions(session, localAddress);

                SetSubscriptions(primeSubscriptions);
            }
        }

        private static Subscriptions LoadSubscriptions(IDocumentSession session)
            => session.Load<Subscriptions>(Subscriptions.SingleDocumentId);

        private static Subscriptions MigrateSubscriptions(IDocumentSession session, Address localAddress)
        {
            logger.Info("Migrating subscriptions to new format");

            var subscriptions = new Subscriptions();

            using (var stream = session.Advanced.Stream<Subscription>("Subscriptions"))
            {
                while (stream.MoveNext())
                {
                    var existingSubscription = stream.Current.Document;
                    existingSubscription.Clients.Remove(localAddress);
                    subscriptions.All.Add(existingSubscription.Id.Replace("Subscriptions/", String.Empty), existingSubscription);
                    session.Advanced.DocumentStore.DatabaseCommands.Delete(stream.Current.Key, null);
                }
            }

            session.Store(subscriptions, Subscriptions.SingleDocumentId);

            session.SaveChanges();
            return subscriptions;
        }

        private static ILog logger = LogManager.GetLogger<SubscriptionPersister>();
    }

    class Subscriptions
    {
        public const string SingleDocumentId = "Subscriptions/All";

        public IDictionary<string, Subscription> All { get; set; } = new Dictionary<string, Subscription>();
    }
}