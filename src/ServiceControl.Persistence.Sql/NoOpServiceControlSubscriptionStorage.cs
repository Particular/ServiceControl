namespace ServiceControl.Persistence.Sql;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Extensibility;
using NServiceBus.Unicast.Subscriptions;
using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;
using ServiceControl.Persistence;

class NoOpServiceControlSubscriptionStorage : IServiceControlSubscriptionStorage
{
    public Task Initialize() => Task.CompletedTask;

    public Task Subscribe(Subscriber subscriber, MessageType messageType, ContextBag context,
        CancellationToken cancellationToken) => Task.CompletedTask;

    public Task Unsubscribe(Subscriber subscriber, MessageType messageType, ContextBag context,
        CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<IEnumerable<Subscriber>> GetSubscriberAddressesForMessage(IEnumerable<MessageType> messageTypes,
        ContextBag context, CancellationToken cancellationToken) =>
        Task.FromResult<IEnumerable<Subscriber>>([]);
}
