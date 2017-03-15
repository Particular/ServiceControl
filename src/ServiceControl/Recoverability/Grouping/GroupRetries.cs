namespace ServiceControl.Recoverability
{
    using NServiceBus;
    using NServiceBus.Features;
    using ServiceControl.Recoverability.Grouping.Api;
    using ServiceControl.Recoverability.Grouping.DomainHandlers;

    public class GroupRetries : Feature
    {
        public GroupRetries()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<OperationManager>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<GroupFetcher>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<PublishAllHandler>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<StoreHistoryHandler>(DependencyLifecycle.SingleInstance);
        }
    }
}
