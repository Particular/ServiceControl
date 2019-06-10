namespace ServiceControl.MessageFailures
{
    using System;
    using System.Threading.Tasks;
    using Api;
    using Handlers;
    using NServiceBus;
    using NServiceBus.Features;
    using Raven.Client;

    class FailedMessagesFeature : Feature
    {
        public FailedMessagesFeature()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<MessageFailureResolvedHandler>(DependencyLifecycle.InstancePerCall);
            context.Container.ConfigureComponent<FailedMessageViewIndexNotifications>(DependencyLifecycle.SingleInstance);

            context.RegisterStartupTask(b => b.Build<WireUpFailedMessageNotifications>());
        }

        class WireUpFailedMessageNotifications : FeatureStartupTask
        {
            public WireUpFailedMessageNotifications(FailedMessageViewIndexNotifications notifications, IDocumentStore store)
            {
                this.notifications = notifications;
                this.store = store;
            }

            protected override Task OnStart(IMessageSession session)
            {
                subscription = store.Changes().ForIndex(new FailedMessageViewIndex().IndexName).Subscribe(notifications);
                return Task.FromResult(true);
            }

            protected override Task OnStop(IMessageSession session)
            {
                subscription.Dispose();
                return Task.FromResult(true);
            }

            FailedMessageViewIndexNotifications notifications;
            IDocumentStore store;
            IDisposable subscription;
        }
    }
}