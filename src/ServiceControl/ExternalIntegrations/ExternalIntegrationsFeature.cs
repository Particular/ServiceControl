namespace ServiceControl.ExternalIntegrations
{
    using NServiceBus.Features;

    class ExternalIntegrationsFeature : Feature
    {
        protected override void Setup(FeatureConfigurationContext context)
            => context.Pipeline.Register(new RemoveVersionInformationBehavior(), "Removes version information from ServiceControl.Contracts messages");
    }
}