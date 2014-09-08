namespace ServiceControl.Infrastructure
{
    using NServiceBus;
    using NServiceBus.Config;
    using NServiceBus.Transports;

    class PrepopulateSubscriptionStorage:IWantToRunWhenConfigurationIsComplete
    {
        public IManageSubscriptions SubscriptionManager { get; set; }

        public void Run()
        {
            
        }

        public void Run(Configure config)
        {
            //todo
            // Setup storage for transports using message driven subscriptions
            //var messageDrivenSubscriptionManager = SubscriptionManager as MessageDrivenSubscriptionManager;
            //if (messageDrivenSubscriptionManager != null)
            //{
            //    config.ForAllTypes<IEvent>(eventType => messageDrivenSubscriptionManager.SubscriptionStorage.Subscribe(Address.Local, new[]
            //    {
            //        new MessageType(eventType)
            //    }));
            //}
        }
    }
}