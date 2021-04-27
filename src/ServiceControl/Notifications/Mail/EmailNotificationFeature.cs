namespace ServiceControl.Notifications.Mail
{
    using NServiceBus;
    using NServiceBus.Features;

    class EmailNotificationFeature : Feature
    {
        public EmailNotificationFeature() => EnableByDefault();

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.RegisterSingleton(new EmailThrottlingState());
            context.Container.ConfigureComponent<CustomChecksMailNotification>(DependencyLifecycle.SingleInstance);
        }
    }
}