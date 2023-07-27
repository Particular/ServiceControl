namespace ServiceControl.Persistence
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

    static class PersistenceHostBuilderExtensions
    {
        public static IHostBuilder SetupPersistence(this IHostBuilder hostBuilder, PersistenceSettings persistenceSettings, IPersistenceConfiguration persistenceConfiguration)
        {
            var persistence = persistenceConfiguration.Create(persistenceSettings);

            hostBuilder.ConfigureServices(serviceCollection =>
            {
                CreatePersisterLifecyle(serviceCollection, persistence);
            });

            return hostBuilder;
        }

        public static void CreatePersisterLifecyle(IServiceCollection serviceCollection, IPersistence persistence)
        {
            var lifecycle = persistence.CreateLifecycle();
            // lifecycle needs to be started before any other hosted service
            serviceCollection.AddHostedService(_ => new PersistenceLifecycleHostedService(lifecycle));
            persistence.Configure(serviceCollection);
        }
    }
}