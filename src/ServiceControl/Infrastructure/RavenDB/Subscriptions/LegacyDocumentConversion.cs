namespace ServiceControl.Infrastructure.RavenDB.Subscriptions
{
    using System.Linq;
    using Raven.Client;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Session;
    using Sparrow.Json;

    static class LegacyDocumentConversion
    {
        internal static void Install(IDocumentStore store)
        {
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
        }

        private static void StoreOnOnAfterConversionToEntity(object sender, AfterConversionToEntityEventArgs e)
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

        private static void BeforeConversionToDocument(object sender, BeforeConversionToDocumentEventArgs e)
        {
            if (!(e.Entity is Subscription subscription))
            {
                return;
            }

            var converted = LegacyAddress.ConvertMultipleToLegacyAddress(subscription.Subscribers);
            subscription.LegacySubscriptions.Clear();
            subscription.LegacySubscriptions.AddRange(converted);
        }
    }
}