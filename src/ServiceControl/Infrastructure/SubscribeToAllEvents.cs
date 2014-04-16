namespace ServiceControl.Infrastructure
{
    using NServiceBus;
    using NServiceBus.Transports;
    using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;

    class SubscribeToAllEvents : IWantToRunWhenBusStartsAndStops
    {
        public IManageSubscriptions SusbcriptionManager { get; set; }

        public void Stop()
        {
        }

        public void Start()
        {
            // Subscribe to events for brokers
            if (!(SusbcriptionManager is MessageDrivenSubscriptionManager))
            {
                Configure.Instance.ForAllTypes<IEvent>(eventType => SusbcriptionManager.Subscribe(eventType, Address.Local));
            }
        }
    }
}
