namespace ServiceControl.Recoverability
{
    using NServiceBus;
    using NServiceBus.Features;

    public class ArchivingFeature : Feature
    {
        public ArchivingFeature()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<ArchivingManager>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<ArchiveDocumentManager>(DependencyLifecycle.SingleInstance);
        }
    }
}