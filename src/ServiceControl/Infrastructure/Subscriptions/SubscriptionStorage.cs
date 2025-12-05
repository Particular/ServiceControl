namespace ServiceControl.Infrastructure.Subscriptions
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.Transport;
    using ServiceControl.Persistence;

    sealed class SubscriptionStorage : Feature
    {
        public SubscriptionStorage() => Prerequisite(c => c.Settings.Get<TransportDefinition>().SupportsPublishSubscribe == false, "The transport supports native pub sub");

        protected override void Setup(FeatureConfigurationContext context) => context.RegisterStartupTask(b => new PrimeSubscriptions(b.GetRequiredService<IServiceControlSubscriptionStorage>()));

        class PrimeSubscriptions(IServiceControlSubscriptionStorage persister) : FeatureStartupTask
        {
            protected override Task OnStart(IMessageSession session, CancellationToken cancellationToken = default) => persister?.Initialize() ?? Task.CompletedTask;

            protected override Task OnStop(IMessageSession session, CancellationToken cancellationToken = default) => Task.CompletedTask;
        }
    }
}