namespace ServiceControl.Operations
{
    using NServiceBus;
    using NServiceBus.Features;

    class FailedErrorImporterFeature : Feature
    {
        public FailedErrorImporterFeature()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<ImportFailedErrors>(DependencyLifecycle.SingleInstance);
        }
    }
}