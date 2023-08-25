namespace ServiceControl.Infrastructure.Subscriptions
{
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.Transport;
    using ServiceControl.Persistence;

    class SubscriptionStorage : Feature
    {
        SubscriptionStorage()
        {
            Prerequisite(c => c.Settings.Get<TransportInfrastructure>().OutboundRoutingPolicy.Publishes == OutboundRoutingType.Unicast, "The transport does not support native pub sub");
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.RegisterStartupTask(b => b.Build<PrimeSubscriptions>());
        }

        class PrimeSubscriptions : FeatureStartupTask
        {
            public IServiceControlSubscriptionStorage persister;

            public PrimeSubscriptions(IServiceControlSubscriptionStorage persister)
            {
                this.persister = persister;
            }

            protected override Task OnStart(IMessageSession session)
            {
                return persister?.Initialize() ?? Task.FromResult(0);
            }

            protected override Task OnStop(IMessageSession session)
            {
                return Task.FromResult(0);
            }
        }
    }
}