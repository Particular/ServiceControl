namespace ServiceControl.Notifications.Email
{
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Features;

    class EmailNotificationFeature : Feature
    {
        public EmailNotificationFeature() => EnableByDefault();

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.RegisterSingleton(new EmailThrottlingState());
            context.Container.ConfigureComponent<CustomChecksMailNotification>(DependencyLifecycle.SingleInstance);

            context.RegisterStartupTask(b => new EmailNotificationStartupTask(b.Build<EmailThrottlingState>()));
        }
    }

    class EmailNotificationStartupTask : FeatureStartupTask
    {
        EmailThrottlingState throttlingState;

        public EmailNotificationStartupTask(EmailThrottlingState throttlingState) => this.throttlingState = throttlingState;

        protected override Task OnStart(IMessageSession session) => Task.CompletedTask;

        protected override Task OnStop(IMessageSession session)
        {
            throttlingState.CancellationTokenSource.Cancel();

            return Task.CompletedTask;
        }
    }
}