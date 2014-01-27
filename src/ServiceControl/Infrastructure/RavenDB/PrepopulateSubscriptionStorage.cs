namespace ServiceControl.Infrastructure.RavenDB
{
    using NServiceBus;
    using NServiceBus.Config;
    using NServiceBus.Unicast.Subscriptions;
    using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;

    class PrepopulateSubscriptionStorage:IWantToRunWhenConfigurationIsComplete
    {
        public ISubscriptionStorage SubscriptionStorage { get; set; }    
        public void Run()
        {
            Configure.Instance.ForAllTypes<IEvent>(eventType => SubscriptionStorage.Subscribe(Address.Local, new[] { new MessageType(eventType)}));
        }
    }
}