namespace ServiceControl.Audit.Recoverability
{
    using NServiceBus;
    using NServiceBus.Features;

    class FailedMessagesFeature : Feature
    {
        public FailedMessagesFeature()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<DetectSuccessfulRetriesEnricher>(DependencyLifecycle.SingleInstance);
        }
    }
}
