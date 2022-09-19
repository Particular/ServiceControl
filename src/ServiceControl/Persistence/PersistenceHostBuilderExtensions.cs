namespace ServiceControl.Persistence
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Raven.Client;
    using Raven.Client.Embedded;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.CustomChecks;
    using ServiceControl.Infrastructure.RavenDB;

    static class PersistenceHostBuilderExtensions
    {
        public static IHostBuilder SetupPersistence(this IHostBuilder hostBuilder, Settings settings)
        {
            var documentStore = new EmbeddableDocumentStore();
            RavenBootstrapper.Configure(documentStore, settings);

            hostBuilder.ConfigureServices(serviceCollection =>
            {
                serviceCollection.AddSingleton<IDocumentStore>(documentStore);
                serviceCollection.AddHostedService<EmbeddedRavenDbHostedService>();
                serviceCollection.AddCustomCheck<CheckRavenDBIndexErrors>();
                //TODO:serviceCollection.AddCustomCheck<CheckRavenDBIndexLag>();

                serviceCollection.AddServiceControlPersistence(settings.DataStoreType);
            });

            return hostBuilder;
        }
    }
}