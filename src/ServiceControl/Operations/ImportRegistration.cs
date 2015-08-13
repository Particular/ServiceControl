namespace ServiceControl.Operations
{
    using NServiceBus;
    using NServiceBus.Features;

    class ImportRegistrationFeature : Feature
    {
        public ImportRegistrationFeature()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            var types = context.Settings.GetAvailableTypes().Implementing<IEnrichImportedMessages>();

            foreach (var type in types)
            {
                context.Container.ConfigureComponent(type, DependencyLifecycle.SingleInstance);
            }
        }
    }
}