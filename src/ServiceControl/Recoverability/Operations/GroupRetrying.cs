namespace ServiceControl.Recoverability
{
    using NServiceBus;
    using NServiceBus.Features;
    using Retrying;

    public class GroupRetrying : Feature
    {
        public GroupRetrying()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<RetryingManager>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<GroupFetcher>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<StoreHistoryHandler>(DependencyLifecycle.SingleInstance);
        }
    }
}
