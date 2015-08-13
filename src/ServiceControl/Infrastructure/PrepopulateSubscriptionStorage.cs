namespace ServiceControl.Infrastructure
{
    using NServiceBus;
    using NServiceBus.Config;
    using NServiceBus.Settings;
    using NServiceBus.Unicast.Subscriptions;
    using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;

    class PrepopulateSubscriptionStorage:IWantToRunWhenConfigurationIsComplete
    {
        public ISubscriptionStorage SubscriptionStorage { get; set; }

        public ReadOnlySettings Settings { get; set; }

        public void Run(Configure configure)
        {
            // Setup storage for transports using message driven subscriptions
            if (SubscriptionStorage != null)
            {
                foreach (var eventType in Settings.GetAvailableTypes().Implementing<IEvent>())
                {
                    SubscriptionStorage.Subscribe(Settings.LocalAddress(), new[]
                    {
                        new MessageType(eventType)
                    });
                }
            }
        }
    }
}