namespace ServiceControl.Infrastructure.Subscriptions
{
    using System.Collections.Generic;
    using System.Linq;
    using NServiceBus;
    using NServiceBus.Persistence.InMemory.SubscriptionStorage;
    using NServiceBus.Unicast.Subscriptions;
    using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;

    public class AlwaysIncludeLocalEndpointStorage:ISubscriptionStorage
    {
        public void Subscribe(Address client, IEnumerable<MessageType> messageTypes)
        {
            innerStorage.Subscribe(client,messageTypes);
        }

        public void Unsubscribe(Address client, IEnumerable<MessageType> messageTypes)
        {
            innerStorage.Unsubscribe(client, messageTypes);
        }

        public IEnumerable<Address> GetSubscriberAddressesForMessage(IEnumerable<MessageType> messageTypes)
        {
            var subscribers = innerStorage.GetSubscriberAddressesForMessage(messageTypes).ToList();

            subscribers.Add(Address.Local);

            return subscribers;
        }

        public void Init()
        {
            
        }

        readonly ISubscriptionStorage innerStorage = new InMemorySubscriptionStorage();

        class Initializer:INeedInitialization
        {
            public void Init()
            {
                Configure.Component<AlwaysIncludeLocalEndpointStorage>(DependencyLifecycle.SingleInstance);
            }
        }
    }
}