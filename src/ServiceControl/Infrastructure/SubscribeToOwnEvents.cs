namespace ServiceControl.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Extensibility;
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

            var routingPolicy = TransportDefinition.GetOutboundRoutingPolicy(Settings);
            if (routingPolicy.Publishes == OutboundRoutingType.Multicast)
            {
                SubscribeForBrokers(eventTypes).GetAwaiter().GetResult();
            }
            else
            {
                SubscribeForNonBrokers(localAddress, eventTypes).GetAwaiter().GetResult();
            }
        }

        async Task SubscribeForBrokers(IEnumerable<Type> eventTypes)
        {
            foreach (var eventType in eventTypes)
            {
                await SubscriptionManager.Subscribe(eventType, new ContextBag()).ConfigureAwait(false);
            }
        }

        async Task SubscribeForNonBrokers(string address, IEnumerable<Type> eventTypes)
        {
            foreach (var eventType in eventTypes)
            {
                await SubscriptionStorage.Subscribe(new Subscriber(address, Settings.EndpointName()), new MessageType(eventType), new ContextBag()).ConfigureAwait(false);
            }
        }

        public IManageSubscriptions SubscriptionManager { get; set; }
        public ISubscriptionStorage SubscriptionStorage { get; set; }
        public ReadOnlySettings Settings { get; set; }
        public TransportDefinition TransportDefinition { get; set; }
    }
}