namespace ServiceControl.Audit.Monitoring
{
    using NServiceBus;
    using NServiceBus.Features;

    class DetectNewEndpointsFromAudits : Feature
    {
        public DetectNewEndpointsFromAudits()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<DetectNewEndpointsFromAuditImportsEnricher>(DependencyLifecycle.SingleInstance);
        }
    }
}