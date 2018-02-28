namespace ServiceControl.Recoverability
{
    using NServiceBus;
    using NServiceBus.Features;
    using ServiceControl.MessageFailures;
    using MessageFailedPublisher = ServiceControl.Operations.MessageFailedPublisher;

    public class RecoverabilityFeature : Feature
    {
        public RecoverabilityFeature()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<MessageFailureResolvedByRetryPublisher>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<MessageFailedPublisher>(DependencyLifecycle.SingleInstance);
        }
    }
}
