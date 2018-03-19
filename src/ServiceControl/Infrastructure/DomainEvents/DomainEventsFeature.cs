namespace ServiceControl.Infrastructure.DomainEvents
{
    using NServiceBus;
    using NServiceBus.Features;

    public class DomainEventsFeature : Feature
    {
        public DomainEventsFeature()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<DomainEventBusPublisher>(DependencyLifecycle.SingleInstance);
        }
    }
}