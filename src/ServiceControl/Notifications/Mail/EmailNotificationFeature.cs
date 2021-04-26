namespace ServiceControl.Notifications.Mail
{
    using System.Threading;
    using NServiceBus;
    using NServiceBus.Features;

    class EmailNotificationFeature : Feature
    {
        public EmailNotificationFeature() => EnableByDefault();

        protected override void Setup(FeatureConfigurationContext context)
        {
            var semaphore = new SemaphoreSlim(1);
            context.Pipeline.Register(b => new EmailNotificationThrottlingBehavior(semaphore),
                "Throttles email sending notification.");

            context.Container.ConfigureComponent<CustomChecksMailNotification>(DependencyLifecycle.SingleInstance);
        }
    }
}