namespace ServiceControl.CustomChecks
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Features;
    using Raven.Client;

    class CustomChecksFeature : Feature
    {
        public CustomChecksFeature()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<CustomCheckNotifications>(DependencyLifecycle.SingleInstance);

            context.RegisterStartupTask(b => b.Build<WireUpCustomCheckNotifications>());
        }

        class WireUpCustomCheckNotifications : FeatureStartupTask
        {
            public WireUpCustomCheckNotifications(CustomCheckNotifications notifications, IDocumentStore store)
            {
                this.notifications = notifications;
                this.store = store;
            }

            protected override Task OnStart(IMessageSession session)
            {
                subscription = store.Changes().ForIndex(new CustomChecksIndex().IndexName).Subscribe(notifications);
                return Task.FromResult(0);
            }

            protected override Task OnStop(IMessageSession session)
            {
                subscription.Dispose();
                return Task.FromResult(0);
            }

            CustomCheckNotifications notifications;
            IDocumentStore store;
            IDisposable subscription;
        }
    }
}