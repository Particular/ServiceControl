namespace ServiceControl.Recoverability
{
    using NServiceBus;
    using NServiceBus.Features;
    using ServiceControl.Recoverability.Grouping.Api;
    using ServiceControl.Recoverability.Grouping.DomainHandlers;

    public class GroupArchiving : Feature
    {
        public GroupArchiving()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<ArchiveDocumentManager>(DependencyLifecycle.SingleInstance);
        }
    }
}
