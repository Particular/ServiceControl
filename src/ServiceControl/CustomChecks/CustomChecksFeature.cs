namespace ServiceControl.CustomChecks
{
    using System;
    using NServiceBus;
    using NServiceBus.Features;
    using Raven.Client;

    class CustomChecksFeature : Feature
    {
        public CustomChecksFeature()
        {
            EnableByDefault();
            RegisterStartupTask<WireUpCustomCheckNotifications>();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<CustomCheckNotifications>(DependencyLifecycle.SingleInstance);
        }

        class WireUpCustomCheckNotifications : FeatureStartupTask
        {
            CustomCheckNotifications notifications;
            IDocumentStore store;
            IDisposable subscription;

            public WireUpCustomCheckNotifications(CustomCheckNotifications notifications, IDocumentStore store)
            {
                this.notifications = notifications;
                this.store = store;
            }

            protected override void OnStart()
            {
                subscription = store.Changes().ForIndex(new CustomChecksIndex().IndexName).Subscribe(notifications);
            }

            protected override void OnStop()
            {
                subscription.Dispose();
            }
        }
    }
}