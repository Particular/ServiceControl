namespace ServiceControl.Recoverability
{
    using NServiceBus;
    using NServiceBus.Features;

    public class RetryingFeature : Feature
    {
        public RetryingFeature()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<RetryingManager>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<GroupFetcher>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<Retrying.StoreHistoryHandler>(DependencyLifecycle.SingleInstance);
        }
    }
}
