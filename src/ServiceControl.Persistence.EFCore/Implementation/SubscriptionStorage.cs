namespace ServiceControl.Persistence.EFCore.Implementation;

using NServiceBus.Extensibility;
using NServiceBus.Unicast.Subscriptions;
using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;

public class SubscriptionStorage : IServiceControlSubscriptionStorage
{
    public Task Initialize() =>
        throw new NotImplementedException();

    public Task Subscribe(Subscriber subscriber, MessageType messageType, ContextBag context, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task Unsubscribe(Subscriber subscriber, MessageType messageType, ContextBag context, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<IEnumerable<Subscriber>> GetSubscriberAddressesForMessage(IEnumerable<MessageType> messageTypes, ContextBag context, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}
