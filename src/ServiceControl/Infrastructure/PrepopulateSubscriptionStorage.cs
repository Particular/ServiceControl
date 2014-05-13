namespace ServiceControl.Infrastructure
{
    using NServiceBus;
    using NServiceBus.Config;
    using NServiceBus.Transports;
    using NServiceBus.Unicast.Subscriptions;
    using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;

    class PrepopulateSubscriptionStorage:IWantToRunWhenConfigurationIsComplete
    {
        public IManageSubscriptions SubscriptionManager { get; set; }

        public void Run()
        {
            // Setup storage for transports using message driven subscriptions
            var messageDrivenSubscriptionManager = SubscriptionManager as MessageDrivenSubscriptionManager;
            if (messageDrivenSubscriptionManager != null)
            {
                Configure.Instance.ForAllTypes<IEvent>(eventType => messageDrivenSubscriptionManager.SubscriptionStorage.Subscribe(Address.Local, new[]
                {
                    new MessageType(eventType)
                }));
            }
        }
    }
}