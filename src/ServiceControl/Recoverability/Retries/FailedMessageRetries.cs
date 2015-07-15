namespace ServiceControl.Recoverability
{
    using NServiceBus;
    using NServiceBus.Features;

    public class FailedMessageRetries : Feature
    {
        public override bool IsEnabledByDefault { get { return true; } }

        public FailedMessageRetries()
        {
            Configure.Component<RetryDocumentManager>(DependencyLifecycle.SingleInstance);
            Configure.Component<RetriesGateway>(DependencyLifecycle.SingleInstance);
        }
    }
}
