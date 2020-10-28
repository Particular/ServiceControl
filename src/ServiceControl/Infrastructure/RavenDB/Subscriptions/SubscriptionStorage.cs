namespace ServiceControl.Infrastructure.RavenDB.Subscriptions
{
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.Settings;
    using NServiceBus.Transport;
    using Raven.Client;
    using Raven.Client.Documents;
    using System.Linq;
    using Raven.Client.Documents.Session;
    using Sparrow.Json;

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
            store.OnBeforeConversionToDocument += BeforeConversionToDocument;
            store.OnAfterConversionToEntity += StoreOnOnAfterConversionToEntity;

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

        private void StoreOnOnAfterConversionToEntity(object sender, AfterConversionToEntityEventArgs e)
        {
            if (!(e.Entity is Subscription subscription))
            {
                return;
            }

            var clients = e.Document["Clients"];

            if (clients != null)
            {
                var converted = LegacyAddress.ParseMultipleToSubscriptionClient(subscription.LegacySubscriptions);

                var legacySubscriptions = converted.Except(subscription.Subscribers).ToArray();
                foreach (var legacySubscription in legacySubscriptions)
                {
                    subscription.Subscribers.Add(legacySubscription);
                }
            }
        }

        private void BeforeConversionToDocument(object sender, BeforeConversionToDocumentEventArgs e)
        {
            if (!(e.Entity is Subscription subscription))
            {
                return;
            }

            var converted = LegacyAddress.ConvertMultipleToLegacyAddress(subscription.Subscribers);
            subscription.LegacySubscriptions.Clear();
            subscription.LegacySubscriptions.AddRange(converted);
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