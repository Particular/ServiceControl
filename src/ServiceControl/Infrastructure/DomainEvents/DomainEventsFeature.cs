namespace ServiceControl.Infrastructure.DomainEvents
{
    using System.Threading.Tasks;
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
            context.RegisterStartupTask(b => b.Build<DomainEventBusPublisher>());
        }
    }
}