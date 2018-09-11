namespace ServiceControl.Infrastructure.DomainEvents
{
    using NServiceBus;
    using NServiceBus.Features;
    using SignalR;

    class ServicePulseNotifierFeature : Feature
    {
        public ServicePulseNotifierFeature()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<ServicePulseNotifier>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<GlobalEventHandler>(DependencyLifecycle.SingleInstance);
        }
    }
}