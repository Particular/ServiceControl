namespace ServiceControl.ExternalIntegrations
{
    using System.Linq;
    using NServiceBus;
    using NServiceBus.Features;
    using ServiceControl.ExternalIntegrations.Config;

    class ExternalIntegrationsFeature : Feature
    {
        public ExternalIntegrationsFeature()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            var adaptersConfig = AdapterConfigurationSection.GetAdapters();

            var adapters = adaptersConfig.Adapters.Cast<AdapterElement>().Select(e => e.Name);

            var integrationEndpoints = adapters.Select(a => $"{a}.integration").ToArray();

            context.Container.ConfigureComponent(b => new IntegrationEventSender(b.Build<IBus>(), integrationEndpoints), DependencyLifecycle.SingleInstance);
        }
    }
}