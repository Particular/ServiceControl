﻿namespace ServiceControl.Infrastructure.RavenDB.Subscriptions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Extensibility;
    using NServiceBus.Logging;
    using NServiceBus.Settings;
    using NServiceBus.Unicast.Subscriptions;
    using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;
    using Raven.Client;

    class SubscriptionPersister : ISubscriptionStorage, IPrimableSubscriptionStorage
    {
        public SubscriptionPersister(IDocumentStore store, ReadOnlySettings settings) :
            this(store, settings.EndpointName(), settings.LocalAddress(), settings.GetAvailableTypes().Implementing<IEvent>().Select(e => new MessageType(e)).ToArray())
        {
        }

        public SubscriptionPersister(IDocumentStore store, string endpointName, string localAddress, MessageType[] locallyHandledEventTypes)
        {
            this.store = store;
            localClient = new SubscriptionClient
            {
                Endpoint = endpointName,
                TransportAddress = localAddress
            };

            this.locallyHandledEventTypes = locallyHandledEventTypes;


            SetSubscriptions(new Subscriptions()).GetAwaiter().GetResult();
        }

        public async Task Prime()
        {
            using (var session = store.OpenAsyncSession())
            {
                var primeSubscriptions = await LoadSubscriptions(session).ConfigureAwait(false) ?? await MigrateSubscriptions(session, localClient).ConfigureAwait(false);

                await SetSubscriptions(primeSubscriptions)
                    .ConfigureAwait(false);
            }
        }

        public async Task Subscribe(Subscriber subscriber, MessageType messageType, ContextBag context)
        {
            if (subscriber.Endpoint == localClient.Endpoint)
            {
                return;
            }

            try
            {
                await subscriptionsLock.WaitAsync().ConfigureAwait(false);

                if (AddOrUpdateSubscription(messageType, subscriber))
                {
                    await SaveSubscriptions().ConfigureAwait(false);
                }
            }
            finally
            {
                subscriptionsLock.Release();
            }
        }

        public async Task Unsubscribe(Subscriber subscriber, MessageType messageType, ContextBag context)
        {
            try
            {
                await subscriptionsLock.WaitAsync().ConfigureAwait(false);

                var needsSave = false;
                if (subscriptions.All.TryGetValue(FormatId(messageType), out var subscription))
                {
                    var client = CreateSubscriptionClient(subscriber);
                    if (subscription.Subscribers.Remove(client))
                    {
                        needsSave = true;
                    }
                }

                if (needsSave)
                {
                    await SaveSubscriptions().ConfigureAwait(false);
                }
            }
            finally
            {
                subscriptionsLock.Release();
            }
        }

        public Task<IEnumerable<Subscriber>> GetSubscriberAddressesForMessage(IEnumerable<MessageType> messageTypes, ContextBag context)
        {
            return Task.FromResult(messageTypes.SelectMany(x => subscriptionsLookup[x]).Distinct());
        }

        bool AddOrUpdateSubscription(MessageType messageType, Subscriber subscriber)
        {
            var key = FormatId(messageType);

            var subscriptionClient = CreateSubscriptionClient(subscriber);

            if (subscriptions.All.TryGetValue(key, out var subscription))
            {
                if (subscription.Subscribers.Contains(subscriptionClient))
                {
                    return false;
                }

                subscription.Subscribers.Add(subscriptionClient);
                return true;
            }

            // New Subscription
            subscription = new Subscription
            {
                Id = key,
                Subscribers = new List<SubscriptionClient>
                {
                    subscriptionClient
                },
                MessageType = messageType
            };
            subscriptions.All.Add(key, subscription);
            return true;
        }

        static SubscriptionClient CreateSubscriptionClient(Subscriber subscriber)
        {
            //When the subscriber is running V6 and UseLegacyMessageDrivenSubscriptionMode is enabled at the subscriber the 'subcriber.Endpoint' value is null
            var endpoint = subscriber.Endpoint ?? subscriber.TransportAddress.Split('@').First();
            var subscriptionClient = new SubscriptionClient
            {
                TransportAddress = subscriber.TransportAddress,
                Endpoint = endpoint
            };
            return subscriptionClient;
        }

        async Task SaveSubscriptions()
        {
            using (var session = store.OpenAsyncSession())
            {
                await session.StoreAsync(subscriptions, Subscriptions.SingleDocumentId)
                    .ConfigureAwait(false);
                UpdateLookup();
                await session.SaveChangesAsync().ConfigureAwait(false);
            }
        }

        void UpdateLookup()
        {
            subscriptionsLookup = (from subscription in subscriptions.All.Values
                                   from client in subscription.Subscribers
                                   select new
                                   {
                                       subscription.MessageType,
                                       Subscriber = new Subscriber(client.TransportAddress, client.Endpoint)
                                   }).Union(from eventType in locallyHandledEventTypes
                                            select new
                                            {
                                                MessageType = eventType,
                                                Subscriber = new Subscriber(localClient.TransportAddress, localClient.Endpoint)
                                            }
            ).ToLookup(x => x.MessageType, x => x.Subscriber);
        }

        string FormatId(MessageType messageType)
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

        async Task SetSubscriptions(Subscriptions newSubscriptions)
        {
            try
            {
                await subscriptionsLock.WaitAsync()
                    .ConfigureAwait(false);

                subscriptions = newSubscriptions;
                UpdateLookup();
            }
            finally
            {
                subscriptionsLock.Release();
            }
        }

        static Task<Subscriptions> LoadSubscriptions(IAsyncDocumentSession session)
            => session.LoadAsync<Subscriptions>(Subscriptions.SingleDocumentId);

        static async Task<Subscriptions> MigrateSubscriptions(IAsyncDocumentSession session, SubscriptionClient localClient)
        {
            logger.Info("Migrating subscriptions to new format");

            var subscriptions = new Subscriptions();

            using (var stream = await session.Advanced.StreamAsync<Subscription>("Subscriptions")
                .ConfigureAwait(false))
            {
                while (await stream.MoveNextAsync().ConfigureAwait(false))
                {
                    var existingSubscription = stream.Current.Document;
                    existingSubscription.Subscribers.Remove(localClient);
                    subscriptions.All.Add(existingSubscription.Id.Replace("Subscriptions/", string.Empty), existingSubscription);
                    await session.Advanced.DocumentStore.AsyncDatabaseCommands.DeleteAsync(stream.Current.Key, null)
                        .ConfigureAwait(false);
                }
            }

            await session.StoreAsync(subscriptions, Subscriptions.SingleDocumentId).ConfigureAwait(false);
            await session.SaveChangesAsync().ConfigureAwait(false);
            return subscriptions;
        }

        IDocumentStore store;
        SubscriptionClient localClient;
        Subscriptions subscriptions;
        ILookup<MessageType, Subscriber> subscriptionsLookup;
        MessageType[] locallyHandledEventTypes;

        SemaphoreSlim subscriptionsLock = new SemaphoreSlim(1);

        static ILog logger = LogManager.GetLogger<SubscriptionPersister>();
    }

    class Subscriptions
    {
        public IDictionary<string, Subscription> All { get; set; } = new Dictionary<string, Subscription>();
        public const string SingleDocumentId = "Subscriptions/All";
    }
}