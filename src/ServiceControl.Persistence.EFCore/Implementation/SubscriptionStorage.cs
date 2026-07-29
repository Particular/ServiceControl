namespace ServiceControl.Persistence.EFCore.Implementation;

using System.Collections.Concurrent;
using Abstractions;
using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus;
using NServiceBus.Extensibility;
using NServiceBus.Settings;
using NServiceBus.Unicast.Subscriptions;
using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;
using ServiceControl.Infrastructure;

public class SubscriptionStorage : DataStoreBase, IServiceControlSubscriptionStorage
{
    public SubscriptionStorage(IServiceScopeFactory scopeFactory, IReadOnlySettings settings, ReceiveAddresses receiveAddresses, EFPersisterSettings persisterSettings, TimeProvider timeProvider)
        : this(scopeFactory, settings.EndpointName(), receiveAddresses.MainReceiveAddress, settings.GetAvailableTypes().Implementing<IEvent>().Select(e => new MessageType(e)).ToArray(), persisterSettings.SubscriptionCacheDuration, timeProvider)
    {
    }

    public SubscriptionStorage(IServiceScopeFactory scopeFactory, string endpointName, string localAddress, MessageType[] locallyHandledEventTypes, TimeSpan cacheDuration, TimeProvider timeProvider)
        : base(scopeFactory)
    {
        localEndpointName = endpointName;
        localSubscriber = new Subscriber(localAddress, endpointName);
        locallyHandledTypeNames = locallyHandledEventTypes.Select(eventType => eventType.TypeName).ToHashSet(StringComparer.Ordinal);
        this.cacheDuration = cacheDuration;
        this.timeProvider = timeProvider;
    }

    // Subscriptions are read on demand, so there is nothing to prime at startup.
    public Task Initialize() => Task.CompletedTask;

    public async Task Subscribe(Subscriber subscriber, MessageType messageType, ContextBag context, CancellationToken cancellationToken = default)
    {
        if (subscriber.Endpoint == localEndpointName)
        {
            return;
        }

        var typeName = messageType.TypeName;
        var transportAddress = subscriber.TransportAddress;
        //When the subscriber is running V6 and UseLegacyMessageDrivenSubscriptionMode is enabled at the subscriber the 'subscriber.Endpoint' value is null
        var endpoint = subscriber.Endpoint ?? transportAddress.Split('@').First();

        await ExecuteWithDbContext(dbContext => dbContext.UpsertAsync(
            [typeName, transportAddress],
            () => new SubscriptionEntity { MessageType = typeName, TransportAddress = transportAddress, Endpoint = endpoint },
            entity => entity.Endpoint = endpoint,
            cancellationToken));

        InvalidateCache(typeName);
    }

    public async Task Unsubscribe(Subscriber subscriber, MessageType messageType, ContextBag context, CancellationToken cancellationToken = default)
    {
        var typeName = messageType.TypeName;
        var transportAddress = subscriber.TransportAddress;

        await ExecuteWithDbContext(dbContext => dbContext.Subscriptions
            .Where(subscription => subscription.MessageType == typeName && subscription.TransportAddress == transportAddress)
            .ExecuteDeleteAsync(cancellationToken));

        InvalidateCache(typeName);
    }

    public async Task<IEnumerable<Subscriber>> GetSubscriberAddressesForMessage(IEnumerable<MessageType> messageTypes, ContextBag context, CancellationToken cancellationToken = default)
    {
        var typeNames = messageTypes.Select(messageType => messageType.TypeName).ToArray();
        var key = string.Join(",", typeNames);

        if (cache.TryGetValue(key, out var cached) && timeProvider.GetUtcNow() - cached.Stored < cacheDuration)
        {
            return cached.Subscribers;
        }

        var subscribers = await LoadSubscribers(typeNames, cancellationToken);
        cache[key] = new CacheItem(timeProvider.GetUtcNow(), typeNames, subscribers);

        return subscribers;
    }

    async Task<Subscriber[]> LoadSubscribers(string[] typeNames, CancellationToken cancellationToken)
    {
        var stored = await ExecuteWithDbContext(dbContext => dbContext.Subscriptions
            .AsNoTracking()
            .Where(subscription => typeNames.Contains(subscription.MessageType))
            .Select(subscription => new { subscription.TransportAddress, subscription.Endpoint })
            .Distinct()
            .ToArrayAsync(cancellationToken));

        var subscribers = stored.Select(subscription => new Subscriber(subscription.TransportAddress, subscription.Endpoint));

        if (locallyHandledTypeNames.Overlaps(typeNames))
        {
            subscribers = subscribers.Append(localSubscriber);
        }

        return subscribers.ToArray();
    }

    void InvalidateCache(string typeName)
    {
        foreach (var entry in cache)
        {
            if (entry.Value.TypeNames.Contains(typeName, StringComparer.Ordinal))
            {
                cache.TryRemove(entry.Key, out _);
            }
        }
    }

    readonly string localEndpointName;
    readonly Subscriber localSubscriber;
    readonly HashSet<string> locallyHandledTypeNames;
    readonly TimeSpan cacheDuration;
    readonly TimeProvider timeProvider;
    readonly ConcurrentDictionary<string, CacheItem> cache = new();

    record CacheItem(DateTimeOffset Stored, string[] TypeNames, Subscriber[] Subscribers);
}
