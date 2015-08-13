namespace ServiceControl.Recoverability
{
    using NServiceBus;
    using NServiceBus.Features;

    public class FailedMessageRetries : Feature
    {
        public FailedMessageRetries()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<RetryDocumentManager>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<RetriesGateway>(DependencyLifecycle.SingleInstance);
        }
    }
}
