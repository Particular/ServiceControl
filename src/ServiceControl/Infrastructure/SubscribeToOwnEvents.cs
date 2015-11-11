namespace ServiceControl.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NServiceBus;
    using NServiceBus.Settings;
    using NServiceBus.Transports;
    using NServiceBus.Unicast.Subscriptions;
    using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;

    class SubscribeToOwnEvents
    {
        public void Run()
        {
            var localAddress = Settings.LocalAddress();
            var eventTypes = Settings.GetAvailableTypes().Implementing<IEvent>();

            if (TransportDefinition.HasNativePubSubSupport)
            {
                SubscribeForBrokers(localAddress, eventTypes);
            }
            else
            {
                SubscribeForNonBrokers(localAddress, eventTypes);
            }
        }

        void SubscribeForBrokers(Address address, IEnumerable<Type> eventTypes)
        {
            foreach (var eventType in eventTypes)
            {
                SubscriptionManager.Subscribe(eventType, address);
            }
        }

        void SubscribeForNonBrokers(Address address, IEnumerable<Type> eventTypes)
        {
            SubscriptionStorage.Subscribe(address, eventTypes.Select(e => new MessageType(e)));
        }

        public IManageSubscriptions SubscriptionManager { get; set; }
        public ISubscriptionStorage SubscriptionStorage { get; set; }
        public ReadOnlySettings Settings { get; set; }
        public TransportDefinition TransportDefinition { get; set; }
    }
}