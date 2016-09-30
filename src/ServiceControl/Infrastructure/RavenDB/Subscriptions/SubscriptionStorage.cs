namespace ServiceControl.Infrastructure.RavenDB.Subscriptions
{
    using NServiceBus;
    using NServiceBus.Features;
    using Raven.Abstractions.Data;
    using Raven.Client.Document;

    class SubscriptionStorage : Feature
    {
        SubscriptionStorage()
        {
            DependsOn<StorageDrivenPublishing>();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            var store = context.Settings.Get<DocumentStore>("ServiceControl.DocumentStore");

            store.Conventions.FindClrType = (id, doc, metadata) =>
            {
                var clrtype = metadata.Value<string>(Constants.RavenClrType);

                // The CLR type cannot be assumed to be always there
                if (clrtype == null)
                {
                    return null;
                }

                if (clrtype.EndsWith(".Subscription, NServiceBus.Core"))
                {
                    clrtype = ReflectionUtil.GetFullNameWithoutVersionInformation(typeof(Subscription));
                }
                else if (clrtype.EndsWith(".Subscription, NServiceBus.RavenDB"))
                {
                    clrtype = ReflectionUtil.GetFullNameWithoutVersionInformation(typeof(Subscription));
                }

                return clrtype;
            };

            context.Container.ConfigureComponent<SubscriptionPersister>(DependencyLifecycle.SingleInstance);
        }
    }
}