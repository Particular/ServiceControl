namespace ServiceControl.Audit.Infrastructure.DomainEvents
{
    using NServiceBus.Features;

    class DomainEventsFeature : Feature
    {
        public DomainEventsFeature()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.RegisterStartupTask(b => b.Build<DomainEventBusPublisher>());
        }
    }
}