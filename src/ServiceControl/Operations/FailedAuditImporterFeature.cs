namespace ServiceControl.Operations
{
    using NServiceBus;
    using NServiceBus.Features;

    public class FailedAuditImporterFeature : Feature
    {
        public FailedAuditImporterFeature()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<ImportFailedAudits>(DependencyLifecycle.SingleInstance);
        }
    }
}