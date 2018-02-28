namespace ServiceControl.Infrastructure.DomainEvents
{
    using NServiceBus;
    using NServiceBus.Features;

    public class ServicePulseNotifierFeature : Feature
    {
        public ServicePulseNotifierFeature()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<ServicePulseNotifier>(DependencyLifecycle.SingleInstance);
        }
    }
}