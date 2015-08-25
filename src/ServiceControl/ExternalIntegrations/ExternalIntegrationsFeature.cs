namespace ServiceControl.ExternalIntegrations
{
    using NServiceBus;
    using NServiceBus.Features;

    class ExternalIntegrationsFeature : Feature
    {
        public ExternalIntegrationsFeature()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            var eventPublisherTypes = context.Settings.GetAvailableTypes().Implementing<IEventPublisher>();

            foreach (var eventPublisherType in eventPublisherTypes)
            {
                context.Container.ConfigureComponent(eventPublisherType, DependencyLifecycle.SingleInstance);
            }
        }
    }
}