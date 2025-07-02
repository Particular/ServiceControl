namespace ServiceControl.Infrastructure.RavenDB.Subscriptions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using NServiceBus;
    using NServiceBus.Extensibility;
    using NServiceBus.Settings;
    using NServiceBus.Unicast.Subscriptions;
    using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;
    using Persistence.RavenDB;
    using Raven.Client.Documents.Commands;
    using Raven.Client.Documents.Session;
    using ServiceControl.Infrastructure;
    using ServiceControl.Persistence;

    class RavenSubscriptionStorage : IServiceControlSubscriptionStorage
    {
        public RavenSubscriptionStorage(IRavenSessionProvider sessionProvider, IReadOnlySettings settings, ReceiveAddresses receiveAddresses, ILogger<RavenSubscriptionStorage> logger) :
            this(sessionProvider, settings.EndpointName(), receiveAddresses.MainReceiveAddress, settings.GetAvailableTypes().Implementing<IEvent>().Select(e => new MessageType(e)).ToArray(), logger)
        {
        }

        public RavenSubscriptionStorage(IRavenSessionProvider sessionProvider, string endpointName, string localAddress, MessageType[] locallyHandledEventTypes, ILogger<RavenSubscriptionStorage> logger)
        {
            this.sessionProvider = sessionProvider;
            localClient = new SubscriptionClient
            {
                Endpoint = endpointName,
                TransportAddress = localAddress
            };

            this.locallyHandledEventTypes = locallyHandledEventTypes;
            this.logger = logger;
            subscriptions = new Subscriptions();
            UpdateLookup();
        }

        public async Task Initialize()
        {
            using var session = await sessionProvider.OpenSession();
            var primeSubscriptions = await LoadSubscriptions(session) ?? await MigrateSubscriptions(session, localClient);

            await SetSubscriptions(primeSubscriptions);
        }

        public async Task Subscribe(Subscriber subscriber, MessageType messageType, ContextBag context, CancellationToken cancellationToken)
        {
            if (subscriber.Endpoint == localClient.Endpoint)
            {
                return;
            }

            try
            {
                await subscriptionsLock.WaitAsync(cancellationToken);

                if (AddOrUpdateSubscription(messageType, subscriber))
                {
                    await SaveSubscriptions();
                }
            }
            finally
            {
                subscriptionsLock.Release();
            }
        }

        public async Task Unsubscribe(Subscriber subscriber, MessageType messageType, ContextBag context, CancellationToken cancellationToken)
        {
            try
            {
                await subscriptionsLock.WaitAsync(cancellationToken);

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
                    await SaveSubscriptions();
                }
            }
            finally
            {
                subscriptionsLock.Release();
            }
        }

        public Task<IEnumerable<Subscriber>> GetSubscriberAddressesForMessage(IEnumerable<MessageType> messageTypes, ContextBag context, CancellationToken cancellationToken)
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
                Subscribers =
                [
                    subscriptionClient
                ],
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
            using var session = await sessionProvider.OpenSession();
            await session.StoreAsync(subscriptions, Subscriptions.SingleDocumentId);
            UpdateLookup();
            await session.SaveChangesAsync();
        }

        void UpdateLookup() =>
            subscriptionsLookup = (from subscription in subscriptions.All.Values
                                   from client in subscription.Subscribers
                                   select new
                                   {
                                       subscription.MessageType,
                                       Subscriber = new Subscriber(client.TransportAddress, client.Endpoint)
                                   }).Union(
                                    from eventType in locallyHandledEventTypes
                                    select new
                                    {
                                        MessageType = eventType,
                                        Subscriber = new Subscriber(localClient.TransportAddress, localClient.Endpoint)
                                    }).ToLookup(x => x.MessageType, x => x.Subscriber);

        string FormatId(MessageType messageType)
        {
            // use MD5 hash to get a 16-byte hash of the string
            var inputBytes = Encoding.Default.GetBytes($"{messageType.TypeName}/{messageType.Version.Major}");
            var hashBytes = MD5.HashData(inputBytes);

            // generate a guid from the hash:
            var id = new Guid(hashBytes);
            return $"Subscriptions/{id}";
        }

        async Task SetSubscriptions(Subscriptions newSubscriptions)
        {
            try
            {
                await subscriptionsLock.WaitAsync();

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

        async Task<Subscriptions> MigrateSubscriptions(IAsyncDocumentSession session, SubscriptionClient localClient)
        {
            logger.LogInformation("Migrating subscriptions to new format");

            var subscriptions = new Subscriptions();

            var stream = await session.Advanced.StreamAsync<Subscription>("Subscriptions");

            while (await stream.MoveNextAsync())
            {
                var existingSubscription = stream.Current.Document;
                existingSubscription.Subscribers.Remove(localClient);
                subscriptions.All.Add(existingSubscription.Id.Replace("Subscriptions/", string.Empty), existingSubscription);
                await session.Advanced.RequestExecutor.ExecuteAsync(new DeleteDocumentCommand(stream.Current.Id, null), session.Advanced.Context);
            }

            await session.StoreAsync(subscriptions, Subscriptions.SingleDocumentId);
            await session.SaveChangesAsync();
            return subscriptions;
        }

        IRavenSessionProvider sessionProvider;
        SubscriptionClient localClient;
        Subscriptions subscriptions;
        ILookup<MessageType, Subscriber> subscriptionsLookup;
        MessageType[] locallyHandledEventTypes;
        readonly ILogger<RavenSubscriptionStorage> logger;
        SemaphoreSlim subscriptionsLock = new SemaphoreSlim(1);
    }

    class Subscriptions
    {
        public IDictionary<string, Subscription> All { get; set; } = new Dictionary<string, Subscription>();
        public const string SingleDocumentId = "Subscriptions/All";
    }
}