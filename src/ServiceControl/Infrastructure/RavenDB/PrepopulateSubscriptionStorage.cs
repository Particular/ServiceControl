namespace ServiceControl.Infrastructure.RavenDB
{
    using NServiceBus;
    using NServiceBus.Config;
    using NServiceBus.Transports;
    using NServiceBus.Unicast.Subscriptions;
    using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;

    class PrepopulateSubscriptionStorage:IWantToRunWhenConfigurationIsComplete
    {
        public IManageSubscriptions SusbcriptionManager { get; set; }

        public void Run()
        {
            var messageDrivenSubscriptionManager = SusbcriptionManager as MessageDrivenSubscriptionManager;
            if (messageDrivenSubscriptionManager != null)
            {
                Configure.Instance.ForAllTypes<IEvent>(eventType => messageDrivenSubscriptionManager.SubscriptionStorage.Subscribe(Address.Local, new[]
                {
                    new MessageType(eventType)
                }));
            }
            else
            {
                Configure.Instance.ForAllTypes<IEvent>(eventType => SusbcriptionManager.Subscribe(eventType, Address.Local));
            }
        }
    }
}