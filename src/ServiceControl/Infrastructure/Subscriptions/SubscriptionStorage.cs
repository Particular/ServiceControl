namespace ServiceControl.Infrastructure.Subscriptions
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.Transport;
    using ServiceControl.Persistence;

    class SubscriptionStorage : Feature
    {
        SubscriptionStorage()
        {
            Prerequisite(c => c.Settings.Get<TransportDefinition>().SupportsPublishSubscribe == false, "The transport supports native pub sub");
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.RegisterStartupTask(b => new PrimeSubscriptions(b.GetRequiredService<IServiceControlSubscriptionStorage>()));
        }

        class PrimeSubscriptions : FeatureStartupTask
        {
            public IServiceControlSubscriptionStorage persister;

            public PrimeSubscriptions(IServiceControlSubscriptionStorage persister)
            {
                this.persister = persister;
            }

            protected override Task OnStart(IMessageSession session, CancellationToken cancellationToken = default)
            {
                return persister?.Initialize() ?? Task.CompletedTask;
            }

            protected override Task OnStop(IMessageSession session, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }
        }
    }
}