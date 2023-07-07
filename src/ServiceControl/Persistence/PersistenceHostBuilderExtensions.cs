namespace ServiceControl.Persistence
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    //using Raven.Client;
    //using Raven.Client.Embedded;
    //using ServiceBus.Management.Infrastructure.Settings;
    //using ServiceControl.CustomChecks;
    //using ServiceControl.Infrastructure.RavenDB;

    static class PersistenceHostBuilderExtensions
    {
        public static IHostBuilder SetupPersistence(this IHostBuilder hostBuilder, PersistenceSettings persistenceSettings, IPersistenceConfiguration persistenceConfiguration)
        {
            var persistence = persistenceConfiguration.Create(persistenceSettings);

            hostBuilder.ConfigureServices(serviceCollection =>
            {
                var lifecycle = persistence.Configure(serviceCollection);

                serviceCollection.AddHostedService(_ => new PersistenceLifecycleHostedService(lifecycle));
            });

            // TODO: Move this stuff to where it belongs in the Raven35 persistence implementation, and remove commented-out usings


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

            return hostBuilder;
        }
    }
}