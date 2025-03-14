namespace ServiceControl.Audit.Persistence.RavenDB
{
    using Microsoft.Extensions.DependencyInjection;
    using Persistence.UnitOfWork;
    using UnitOfWork;

    class RavenPersistence(DatabaseConfiguration databaseConfiguration) : IPersistence
    {
        public void AddPersistence(IServiceCollection services)
        {
            ConfigureLifecycle(services, databaseConfiguration);

            services.AddSingleton<IAuditDataStore, RavenAuditDataStore>();
            services.AddSingleton<IAuditIngestionUnitOfWorkFactory, RavenAuditIngestionUnitOfWorkFactory>();
            services.AddSingleton<IFailedAuditStorage, RavenFailedAuditStorage>();
            services.AddSingleton<MinimumRequiredStorageState>();
        }

        public void AddInstaller(IServiceCollection services) => ConfigureLifecycle(services, databaseConfiguration);

        static void ConfigureLifecycle(IServiceCollection services, DatabaseConfiguration databaseConfiguration)
        {
            services.AddSingleton(databaseConfiguration);
            services.AddSingleton<MemoryInformationRetriever>();

            services.AddSingleton<IRavenSessionProvider, RavenSessionProvider>();
            services.AddHostedService<RavenPersistenceLifecycleHostedService>();

            var serverConfiguration = databaseConfiguration.ServerConfiguration;
            if (serverConfiguration.UseEmbeddedServer)
            {
                services.AddSingleton<RavenEmbeddedPersistenceLifecycle>();
                services.AddSingleton<IRavenPersistenceLifecycle>(provider => provider.GetRequiredService<RavenEmbeddedPersistenceLifecycle>());
                services.AddSingleton<IRavenDocumentStoreProvider>(provider => provider.GetRequiredService<RavenEmbeddedPersistenceLifecycle>());
                return;
            }

            services.AddSingleton<RavenExternalPersistenceLifecycle>();
            services.AddSingleton<IRavenPersistenceLifecycle>(provider => provider.GetRequiredService<RavenExternalPersistenceLifecycle>());
            services.AddSingleton<IRavenDocumentStoreProvider>(provider => provider.GetRequiredService<RavenExternalPersistenceLifecycle>());
        }
    }
}