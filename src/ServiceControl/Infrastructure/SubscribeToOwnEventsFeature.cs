namespace ServiceControl.Infrastructure
{
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.Settings;
    using NServiceBus.Transports;
    using NServiceBus.Unicast.Subscriptions;
    using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;

    class SubscribeToOwnEventsFeature : Feature
    {
        public SubscribeToOwnEventsFeature()
        {
            EnableByDefault();
            RegisterStartupTask<PrepopulateSubscriptionStorage>();
            RegisterStartupTask<SubscribeToAllEventsForBrokers>();
        }

        protected override void Setup(FeatureConfigurationContext context) { }

        class PrepopulateSubscriptionStorage : FeatureStartupTask
        {
            protected override void OnStart()
            {
                if (!TransportDefinition.HasNativePubSubSupport)
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

            public ISubscriptionStorage SubscriptionStorage { get; set; }
            public ReadOnlySettings Settings { get; set; }
            public TransportDefinition TransportDefinition { get; set; }
        }

        class SubscribeToAllEventsForBrokers : FeatureStartupTask
        {
            protected override void OnStart()
            {
                if (TransportDefinition.HasNativePubSubSupport)
                {
                    foreach (var eventType in Settings.GetAvailableTypes().Implementing<IEvent>())
                    {
                        SubscriptionManager.Subscribe(eventType, Settings.LocalAddress());
                    }
                }
            }

            public IManageSubscriptions SubscriptionManager { get; set; }
            public ReadOnlySettings Settings { get; set; }
            public TransportDefinition TransportDefinition { get; set; }
        }
    }
}