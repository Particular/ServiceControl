namespace ServiceControl.Recoverability
{
    using NServiceBus;
    using NServiceBus.Features;

    public class GroupArchiving : Feature
    {
        public GroupArchiving()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<RetryingManager>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<ArchiveDocumentManager>(DependencyLifecycle.SingleInstance);
        }
    }
}
