namespace ServiceControl.Infrastructure
{
    using NServiceBus;
    using NServiceBus.Settings;
    using NServiceBus.Transports;

    class SubscribeToAllEvents : IWantToRunWhenBusStartsAndStops
    {
        public IManageSubscriptions SubscriptionManager { get; set; }

        public ReadOnlySettings Settings { get; set; }

        public TransportDefinition TransportDefinition { get; set; }

        public void Stop()
        {
        }

        public void Start()
        {
            // Subscribe to events for brokers
            if(TransportDefinition.HasNativePubSubSupport)
            {
                foreach (var eventType in Settings.GetAvailableTypes().Implementing<IEvent>())
                {
                    SubscriptionManager.Subscribe(eventType, Settings.LocalAddress());
                }
            }
        }
    }
}
