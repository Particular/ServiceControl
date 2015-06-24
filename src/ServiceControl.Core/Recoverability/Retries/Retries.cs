namespace ServiceControl.Recoverability.Retries
{
    using NServiceBus;
    using NServiceBus.Features;

    public class Retries : Feature
    {
        public override bool IsEnabledByDefault { get { return true; } }

        public override void Initialize()
        {
            Configure.Component<Retryer>(DependencyLifecycle.SingleInstance);
            Configure.Component<RetryProcessor>(DependencyLifecycle.SingleInstance);
        }
    }
}
