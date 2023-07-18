namespace ServiceControl.Persistence.RavenDb
{
    using System.Collections.ObjectModel;
    using Microsoft.Extensions.DependencyInjection;
    using Raven.Client;
    using Raven.Client.Embedded;
    using ServiceControl.CustomChecks;
    using ServiceControl.Infrastructure.RavenDB.Subscriptions;
    using ServiceControl.MessageFailures;
    using ServiceControl.Operations.BodyStorage;
    using ServiceControl.Operations.BodyStorage.RavenAttachments;
    using ServiceControl.Persistence.RavenDb.SagaAudit;
    using ServiceControl.Persistence.UnitOfWork;

    class RavenDbPersistence : IPersistence
    {
        public RavenDbPersistence(PersistenceSettings settings, EmbeddableDocumentStore documentStore, RavenStartup ravenStartup)
        {
            this.settings = settings;
            this.documentStore = documentStore;
            this.ravenStartup = ravenStartup;
        }

        public IPersistenceLifecycle Configure(IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton(settings);
            serviceCollection.AddSingleton<IDocumentStore>(documentStore);

            serviceCollection.AddSingleton<IServiceControlSubscriptionStorage, RavenDbSubscriptionStorage>();
            serviceCollection.AddSingleton<IMonitoringDataStore, RavenDbMonitoringDataStore>();
            serviceCollection.AddSingleton<ICustomChecksDataStore, RavenDbCustomCheckDataStore>();
            serviceCollection.AddUnitOfWorkFactory<RavenDbIngestionUnitOfWorkFactory>();
            serviceCollection.AddSingleton<MinimumRequiredStorageState>();
            serviceCollection.AddSingleton<IBodyStorage, RavenAttachmentsBodyStorage>();

            serviceCollection.AddSingleton<IFailedMessageViewIndexNotifications>(p => p.GetRequiredService<FailedMessageViewIndexNotifications>());
            serviceCollection.AddSingleton<FailedMessageViewIndexNotifications>();
            serviceCollection.AddHostedService(p => p.GetRequiredService<FailedMessageViewIndexNotifications>());

            serviceCollection.AddSingleton<ExternalIntegrationRequestsDataStore>();
            serviceCollection.AddSingleton<IExternalIntegrationRequestsDataStore, ExternalIntegrationRequestsDataStore>();
            serviceCollection.AddHostedService(p => p.GetRequiredService<ExternalIntegrationRequestsDataStore>());


            // TODO: Find where these extension methods came from, worried there might be some circular references
            //serviceCollection.AddCustomCheck<CheckRavenDBIndexErrors>();
            //serviceCollection.AddCustomCheck<CheckRavenDBIndexLag>();
            serviceCollection.AddCustomCheck<AuditRetentionCustomCheck>();

            //serviceCollection.AddServiceControlPersistence(settings.DataStoreType);

            return new RavenDbPersistenceLifecycle(ravenStartup, documentStore);
        }

        // TODO: Make sure this stuff from PersistenceHostBuilderExtensions is accounted for here

        //var documentStore = new EmbeddableDocumentStore();
        //RavenBootstrapper.Configure(documentStore, settings);

        //hostBuilder.ConfigureServices(serviceCollection =>
        //{
        //    serviceCollection.AddSingleton<IDocumentStore>(documentStore);
        //    serviceCollection.AddHostedService<EmbeddedRavenDbHostedService>();
        //    serviceCollection.AddCustomCheck<CheckRavenDBIndexErrors>();
        //    serviceCollection.AddCustomCheck<CheckRavenDBIndexLag>();

        //    serviceCollection.AddServiceControlPersistence(settings.DataStoreType);
        //});

        public IPersistenceInstaller CreateInstaller()
        {
            return new RavenDbInstaller(documentStore, ravenStartup);
        }

        readonly RavenStartup ravenStartup;
        readonly PersistenceSettings settings;
        readonly EmbeddableDocumentStore documentStore;
    }
}
