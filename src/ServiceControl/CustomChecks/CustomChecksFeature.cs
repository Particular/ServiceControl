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
            context.RegisterStartupTask(builder => builder.Build<WireUpCustomCheckNotifications>());
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

            protected override Task OnStart(IBusSession session)
            {
                subscription = store.Changes().ForIndex(new CustomChecksIndex().IndexName).Subscribe(notifications);
                return Task.FromResult(0);
            }

            protected override Task OnStop(IBusSession session)
            {
                subscription.Dispose();
                return Task.FromResult(0);
            }
        }
    }
}