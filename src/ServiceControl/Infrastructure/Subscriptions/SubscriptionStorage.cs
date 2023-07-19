namespace ServiceControl.Infrastructure.Subscriptions
{
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.Settings;
    using NServiceBus.Transport;
    using ServiceControl.Persistence;

    class SubscriptionStorage : Feature
    {
        public SubscriptionStorage()
        {
            Prerequisite(c => c.Settings.Get<TransportInfrastructure>().OutboundRoutingPolicy.Publishes == OutboundRoutingType.Unicast || IsMigrationModeEnabled(c.Settings), "The transport supports native pub sub");
        }

        static bool IsMigrationModeEnabled(ReadOnlySettings settings)
        {
            return settings.TryGet("NServiceBus.Subscriptions.EnableMigrationMode", out bool enabled) && enabled;
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            // TODO: As it is now, I think each Persistence project would just need to know it has to register a IServiceControlSubscriptionStorage implementation.
            // Having trouble making it more elegant given the disconnect between the NServiceBus setup and the app's persistence setup
            context.Container.ConfigureComponent<IServiceControlSubscriptionStorage>(DependencyLifecycle.SingleInstance);
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