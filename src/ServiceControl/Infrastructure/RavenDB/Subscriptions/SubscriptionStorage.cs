namespace ServiceControl.Infrastructure.RavenDB.Subscriptions
{
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.Settings;
    using NServiceBus.Transport;

    class SubscriptionStorage : Feature
    {
        SubscriptionStorage()
        {
            Prerequisite(c => c.Settings.Get<TransportInfrastructure>().OutboundRoutingPolicy.Publishes == OutboundRoutingType.Unicast || IsMigrationModeEnabled(c.Settings), "The transport supports native pub sub");
        }

        static bool IsMigrationModeEnabled(ReadOnlySettings settings)
        {
            return settings.TryGet("NServiceBus.Subscriptions.EnableMigrationMode", out bool enabled) && enabled;
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            //TODO: RAVEN5 missing API RavenClrType
            //var store = context.Settings.Get<IDocumentStore>();

            // store.Conventions.FindClrType = (id, doc, metadata) =>
            // {
            //     var clrtype = metadata.Value<string>(Constants.RavenClrType);
            //
            //     // The CLR type cannot be assumed to be always there
            //     if (clrtype == null)
            //     {
            //         return null;
            //     }
            //
            //     if (clrtype.EndsWith(".Subscription, NServiceBus.Core"))
            //     {
            //         clrtype = ReflectionUtil.GetFullNameWithoutVersionInformation(typeof(Subscription));
            //     }
            //     else if (clrtype.EndsWith(".Subscription, NServiceBus.RavenDB"))
            //     {
            //         clrtype = ReflectionUtil.GetFullNameWithoutVersionInformation(typeof(Subscription));
            //     }
            //
            //     return clrtype;
            // };
            context.Container.ConfigureComponent<SubscriptionPersister>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<PrimeSubscriptions>(DependencyLifecycle.SingleInstance);
            context.RegisterStartupTask(b => b.Build<PrimeSubscriptions>());
        }

        class PrimeSubscriptions : FeatureStartupTask
        {
            public IPrimableSubscriptionStorage Persister { get; set; }

            protected override Task OnStart(IMessageSession session)
            {
                return Persister?.Prime() ?? Task.FromResult(0);
            }

            protected override Task OnStop(IMessageSession session)
            {
                return Task.FromResult(0);
            }
        }
    }
}