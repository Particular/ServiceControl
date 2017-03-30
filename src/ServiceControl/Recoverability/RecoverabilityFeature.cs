namespace ServiceControl.Recoverability
{
    using NServiceBus;
    using NServiceBus.Features;

    public class RecoverabilityFeature : Feature
    {
        public RecoverabilityFeature()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<PublishAllHandler>(DependencyLifecycle.SingleInstance);
        }
    }
}
