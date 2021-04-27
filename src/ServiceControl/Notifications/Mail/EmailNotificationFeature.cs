namespace ServiceControl.Notifications.Mail
{
    using System.Threading;
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
        }
    }

    public class EmailThrottlingState
    {
        SemaphoreSlim semaphore = new SemaphoreSlim(1);
        int latestFailureNumber = 0;

        public int NextFailure() => Interlocked.Increment(ref latestFailureNumber);

        public Task Wait() => semaphore.WaitAsync();

        public void Release() => semaphore.Release(1);

        public bool IsLatestFailure(int failureNumber) => Volatile.Read(ref latestFailureNumber) <= failureNumber;
    }
}