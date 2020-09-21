using Sparrow.Json;

namespace ServiceControl.Infrastructure.RavenDB.Subscriptions
{
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.Settings;
    using NServiceBus.Transport;
    using Raven.Client;
    using Raven.Client.Documents;
    using Raven.Client.Util;

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
            var store = context.Settings.Get<IDocumentStore>();
            store.Conventions.FindClrType = (id, doc) =>
            {
                if (doc.TryGet(Constants.Documents.Metadata.Key, out BlittableJsonReaderObject metadata) &&
                    metadata.TryGet(Constants.Documents.Metadata.RavenClrType, out string clrType))
                {
                    // The CLR type cannot be assumed to be always there
                    if (clrType == null)
                    {
                        return null;
                    }

                    //TODO:RAVEN5 Need to add a test that handles subscription types with wrong metadata
                    if (clrType.EndsWith(".Subscription, NServiceBus.Core"))
                    {
                        clrType = $"{typeof(Subscription).FullName}, {typeof(Subscription).Assembly.GetName().Name}";
                    }
                    else if (clrType.EndsWith(".Subscription, NServiceBus.RavenDB"))
                    {
                        clrType = $"{typeof(Subscription).FullName}, {typeof(Subscription).Assembly.GetName().Name}";
                    }

                    return clrType;
                }

                return null;
            };
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