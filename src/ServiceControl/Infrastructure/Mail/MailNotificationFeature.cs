namespace ServiceControl.Infrastructure.Mail
{
    using NServiceBus;
    using NServiceBus.Features;
    using Raven.Client;

    class MailNotificationFeature : Feature
    {
        public MailNotificationFeature()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent(b => new CustomChecksMailNotification(b.Build<IDocumentStore>()), DependencyLifecycle.SingleInstance);
        }
    }
}