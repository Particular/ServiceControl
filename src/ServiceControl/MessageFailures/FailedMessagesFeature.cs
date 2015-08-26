namespace ServiceControl.MessageFailures
{
    using System;
    using NServiceBus;
    using NServiceBus.Features;
    using Raven.Client;
    using ServiceControl.MessageFailures.Api;

    public class FailedMessagesFeature : Feature
    {
        public FailedMessagesFeature()
        {
            EnableByDefault();
            RegisterStartupTask<WireUpFailedMessageNotifications>();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<FailedMessageViewIndexNotifications>(DependencyLifecycle.SingleInstance);
        }

        class WireUpFailedMessageNotifications : FeatureStartupTask
        {
            FailedMessageViewIndexNotifications notifications;
            IDocumentStore store;
            IDisposable subscription;

            public WireUpFailedMessageNotifications(FailedMessageViewIndexNotifications notifications, IDocumentStore store)
            {
                this.notifications = notifications;
                this.store = store;
            }

            protected override void OnStart()
            {
                subscription = store.Changes().ForIndex(new FailedMessageViewIndex().IndexName).Subscribe(notifications);
            }

            protected override void OnStop()
            {
                subscription.Dispose();
            }
        }
    }
}