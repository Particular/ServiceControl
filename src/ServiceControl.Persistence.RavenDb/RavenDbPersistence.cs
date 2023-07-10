namespace ServiceControl.Persistence.RavenDb
{
    using Microsoft.Extensions.DependencyInjection;
    using Raven.Client;
    using Raven.Client.Embedded;
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


            serviceCollection.AddSingleton<IMonitoringDataStore, RavenDbMonitoringDataStore>();
            serviceCollection.AddSingleton<ICustomChecksDataStore, RavenDbCustomCheckDataStore>();
            serviceCollection.AddUnitOfWorkFactory<RavenDbIngestionUnitOfWorkFactory>();
            serviceCollection.AddSingleton<MinimumRequiredStorageState>();


            // TODO: Find where these extension methods came from, worried there might be some circular references
            //serviceCollection.AddCustomCheck<CheckRavenDBIndexErrors>();
            //serviceCollection.AddCustomCheck<CheckRavenDBIndexLag>();

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

        readonly PersistenceSettings settings;
        readonly EmbeddableDocumentStore documentStore;
        readonly RavenStartup ravenStartup;
    }
}
