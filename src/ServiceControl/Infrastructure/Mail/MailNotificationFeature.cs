namespace ServiceControl.Infrastructure.Mail
{
    using NServiceBus;
    using NServiceBus.Features;

    class MailNotificationFeature : Feature
    {
        public MailNotificationFeature()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<CustomChecksMailNotification>(DependencyLifecycle.SingleInstance);
        }
    }
}