namespace ServiceControl.Infrastructure
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.Routing.MessageDrivenSubscriptions;
    using NServiceBus.Transport;

    public class SubscriptionFeature : Feature
    {
        protected override void Setup(FeatureConfigurationContext context)
        {
            var remoteAddresses = context.Settings.Get<string[]>("ServiceControl.RemoteInstances");
            var typesToSubscribeTo = context.Settings.Get<Type[]>("ServiceControl.RemoteTypesToSubscribeTo");

            var transportInfrastructure = context.Settings.Get<TransportInfrastructure>();
            if (transportInfrastructure.OutboundRoutingPolicy.Publishes == OutboundRoutingType.Unicast)
            {
                var publishers = context.Settings.Get<Publishers>();

                var remotePublishers = remoteAddresses.SelectMany(r => typesToSubscribeTo.Select(t => new PublisherTableEntry(t, PublisherAddress.CreateFromPhysicalAddresses(r))));
                var localAddress = context.Settings.LocalAddress();
                var publisherRoutes = remotePublishers.Concat(typesToSubscribeTo.Select(t => new PublisherTableEntry(t, PublisherAddress.CreateFromPhysicalAddresses(localAddress))));

                publishers.AddOrReplacePublishers("ServiceControl", publisherRoutes.ToList());
            }

            context.RegisterStartupTask(new SubscriptionStartupTask(typesToSubscribeTo));
        }
    }

    class SubscriptionStartupTask : FeatureStartupTask
    {
        private readonly Type[] localEventsToSubscribeTo;

        public SubscriptionStartupTask(Type[] localEventsToSubscribeTo)
        {
            this.localEventsToSubscribeTo = localEventsToSubscribeTo;
        }

        protected override Task OnStart(IMessageSession session)
        {
            return Task.WhenAll(localEventsToSubscribeTo.Select(session.Subscribe));
        }

        protected override Task OnStop(IMessageSession session)
        {
            return Task.FromResult(0);
        }
    }
}