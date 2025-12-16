namespace ServiceControl.Persistence.Sql.Core.Implementation;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Entities;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus.Extensibility;
using NServiceBus.Settings;
using NServiceBus.Unicast.Subscriptions;
using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;
using ServiceControl.Infrastructure;
using ServiceControl.Persistence;

public class ServiceControlSubscriptionStorage : DataStoreBase, IServiceControlSubscriptionStorage
{
    readonly SubscriptionClient localClient;
    readonly MessageType[] locallyHandledEventTypes;
    ILookup<MessageType, Subscriber> subscriptionsLookup = Enumerable.Empty<MessageType>().ToLookup(x => x, x => new Subscriber("", ""));
    readonly SemaphoreSlim subscriptionsLock = new SemaphoreSlim(1);

    public ServiceControlSubscriptionStorage(
        IServiceScopeFactory scopeFactory,
        IReadOnlySettings settings,
        ReceiveAddresses receiveAddresses)
        : this(
            scopeFactory,
            settings.EndpointName(),
            receiveAddresses.MainReceiveAddress,
            settings.GetAvailableTypes().Implementing<IEvent>().Select(e => new MessageType(e)).ToArray())
    {
    }

    public ServiceControlSubscriptionStorage(
        IServiceScopeFactory scopeFactory,
        string endpointName,
        string localAddress,
        MessageType[] locallyHandledEventTypes) : base(scopeFactory)
    {
        localClient = new SubscriptionClient
        {
            Endpoint = endpointName,
            TransportAddress = localAddress
        };
        this.locallyHandledEventTypes = locallyHandledEventTypes;
    }

    public Task Initialize()
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var subscriptions = await dbContext.Subscriptions
                .AsNoTracking()
                .ToListAsync();

            UpdateLookup(subscriptions);
        });
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

            await ExecuteWithDbContext(async dbContext =>
            {
                var subscriptionId = FormatId(messageType);
                var subscriptionClient = CreateSubscriptionClient(subscriber);

                var subscription = await dbContext.Subscriptions
                    .FirstOrDefaultAsync(s => s.Id == subscriptionId, cancellationToken);

                if (subscription == null)
                {
                    subscription = new SubscriptionEntity
                    {
                        Id = subscriptionId,
                        MessageTypeTypeName = messageType.TypeName,
                        MessageTypeVersion = messageType.Version.Major,
                        SubscribersJson = JsonSerializer.Serialize(new List<SubscriptionClient> { subscriptionClient }, JsonSerializationOptions.Default)
                    };
                    await dbContext.Subscriptions.AddAsync(subscription, cancellationToken);
                }
                else
                {
                    var subscribers = JsonSerializer.Deserialize<List<SubscriptionClient>>(subscription.SubscribersJson, JsonSerializationOptions.Default) ?? [];
                    if (!subscribers.Contains(subscriptionClient))
                    {
                        subscribers.Add(subscriptionClient);
                        subscription.SubscribersJson = JsonSerializer.Serialize(subscribers, JsonSerializationOptions.Default);
                    }
                    else
                    {
                        // Already subscribed, no need to save
                        return;
                    }
                }

                await dbContext.SaveChangesAsync(cancellationToken);

                // Refresh lookup
                var allSubscriptions = await dbContext.Subscriptions.AsNoTracking().ToListAsync(cancellationToken);
                UpdateLookup(allSubscriptions);
            });
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

            await ExecuteWithDbContext(async dbContext =>
            {
                var subscriptionId = FormatId(messageType);
                var subscription = await dbContext.Subscriptions
                    .FirstOrDefaultAsync(s => s.Id == subscriptionId, cancellationToken);

                if (subscription != null)
                {
                    var subscriptionClient = CreateSubscriptionClient(subscriber);
                    var subscribers = JsonSerializer.Deserialize<List<SubscriptionClient>>(subscription.SubscribersJson, JsonSerializationOptions.Default) ?? [];

                    if (subscribers.Remove(subscriptionClient))
                    {
                        subscription.SubscribersJson = JsonSerializer.Serialize(subscribers, JsonSerializationOptions.Default);
                        await dbContext.SaveChangesAsync(cancellationToken);

                        // Refresh lookup
                        var allSubscriptions = await dbContext.Subscriptions.AsNoTracking().ToListAsync(cancellationToken);
                        UpdateLookup(allSubscriptions);
                    }
                }
            });
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

    void UpdateLookup(List<SubscriptionEntity> subscriptions)
    {
        subscriptionsLookup = (from subscription in subscriptions
                               let subscribers = JsonSerializer.Deserialize<List<SubscriptionClient>>(subscription.SubscribersJson, JsonSerializationOptions.Default) ?? []
                               from client in subscribers
                               select new
                               {
                                   MessageType = new MessageType(subscription.MessageTypeTypeName, new Version(subscription.MessageTypeVersion, 0)),
                                   Subscriber = new Subscriber(client.TransportAddress, client.Endpoint)
                               }).Union(
                                from eventType in locallyHandledEventTypes
                                select new
                                {
                                    MessageType = eventType,
                                    Subscriber = new Subscriber(localClient.TransportAddress, localClient.Endpoint)
                                }).ToLookup(x => x.MessageType, x => x.Subscriber);
    }

    static SubscriptionClient CreateSubscriptionClient(Subscriber subscriber)
    {
        //When the subscriber is running V6 and UseLegacyMessageDrivenSubscriptionMode is enabled at the subscriber the 'subcriber.Endpoint' value is null
        var endpoint = subscriber.Endpoint ?? subscriber.TransportAddress.Split('@').First();
        return new SubscriptionClient
        {
            TransportAddress = subscriber.TransportAddress,
            Endpoint = endpoint
        };
    }

    string FormatId(MessageType messageType)
    {
        // use MD5 hash to get a 16-byte hash of the string
        var inputBytes = Encoding.Default.GetBytes($"{messageType.TypeName}/{messageType.Version.Major}");
        var hashBytes = MD5.HashData(inputBytes);

        // generate a guid from the hash:
        var id = new Guid(hashBytes);
        return id.ToString();
    }

    class SubscriptionClient
    {
        public string TransportAddress { get; set; } = null!;
        public string Endpoint { get; set; } = null!;

        public override bool Equals(object? obj)
        {
            if (obj is null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return obj is SubscriptionClient client && Equals(client);
        }

        bool Equals(SubscriptionClient obj) =>
            string.Equals(TransportAddress, obj.TransportAddress, StringComparison.InvariantCultureIgnoreCase);

        public override int GetHashCode() => TransportAddress.ToLowerInvariant().GetHashCode();
    }
}
