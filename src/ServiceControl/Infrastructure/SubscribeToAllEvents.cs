namespace ServiceControl.Infrastructure
{
    using System.Linq;
    using NServiceBus;
    using NServiceBus.Settings;
    using NServiceBus.Transports;

    class SubscribeToAllEvents : IWantToRunWhenBusStartsAndStops
    {
        public IManageSubscriptions SubscriptionManager { get; set; }

        public void Stop()
        {
        }

        public ReadOnlySettings ReadOnlySettings { get; set; }

        public Configure Configure { get; set; } 

        public void Start()
        {
            // Subscribe to events for brokers
            if (!(SubscriptionManager.GetType().Name.Contains("MessageDriven"))) //todo
            {
                foreach (var eventType in ReadOnlySettings.GetAvailableTypes()
                   .Where(t => typeof(IEvent).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface))
                {
                    SubscriptionManager.Subscribe(eventType, Configure.LocalAddress);
                }
            }
        }
    }
}
