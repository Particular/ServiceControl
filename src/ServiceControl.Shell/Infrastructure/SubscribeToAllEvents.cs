namespace ServiceControl.Infrastructure
{
    using NServiceBus;
    using NServiceBus.Transports;
    using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;

    class SubscribeToAllEvents : IWantToRunWhenBusStartsAndStops
    {
        public IManageSubscriptions SubscriptionManager { get; set; }

        public void Stop()
        {
        }

        public void Start()
        {
            // Subscribe to events for brokers
            if (!(SubscriptionManager is MessageDrivenSubscriptionManager))
            {
                Configure.Instance.ForAllTypes<IEvent>(eventType => SubscriptionManager.Subscribe(eventType, Address.Local));
            }
        }
    }
}
