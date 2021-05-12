namespace ServiceControl.ExternalIntegrations
{
    using NServiceBus;
    using NServiceBus.Features;
    using ServiceBus.Management.Infrastructure.Settings;

    class ExternalIntegrationsFeature : Feature
    {
        public ExternalIntegrationsFeature()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.RegisterStartupTask(b => b.Build<EventDispatcher>());

            var eventPublisherTypes = context.Settings.GetAvailableTypes().Implementing<IEventPublisher>();

            foreach (var eventPublisherType in eventPublisherTypes)
            {
                context.Container.ConfigureComponent(eventPublisherType, DependencyLifecycle.SingleInstance);
            }

            context.Container.ConfigureComponent<IntegrationEventWriter>(DependencyLifecycle.SingleInstance);

            //This should be removed in the next major version when we make the switch to ServiceControl.Contracts ver 3.0
            var serviceControlSettings = context.Settings.Get<Settings>("ServiceControl.Settings");

            if (serviceControlSettings.EnableV3IntegrationEvents == false)
            {
                context.Pipeline.Register(new FallbackToOldContractTypes(),
                    "Overrides ServiceControl.Contracts ver 3.0 assembly identifier with ver 1.0 assembly identifier.");
            }
        }
    }
}