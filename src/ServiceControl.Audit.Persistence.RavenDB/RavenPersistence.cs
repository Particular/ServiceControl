namespace ServiceControl.Audit.Persistence.RavenDB
{
    using Microsoft.Extensions.DependencyInjection;
    using Persistence.UnitOfWork;
    using RavenDB.CustomChecks;
    using UnitOfWork;

    class RavenPersistence(DatabaseConfiguration databaseConfiguration) : IPersistence
    {
        public void Configure(IServiceCollection services)
        {
            services.AddSingleton(databaseConfiguration);
            services.AddSingleton<IRavenSessionProvider, RavenSessionProvider>();
            services.AddSingleton<IAuditDataStore, RavenAuditDataStore>();
            services.AddSingleton<IAuditIngestionUnitOfWorkFactory, RavenAuditIngestionUnitOfWorkFactory>();
            services.AddSingleton<IFailedAuditStorage, RavenFailedAuditStorage>();
            services.AddSingleton<CheckMinimumStorageRequiredForAuditIngestion.State>();

            ConfigureLifecycle(services);
        }

        void ConfigureLifecycle(IServiceCollection services)
        {
            var serverConfiguration = databaseConfiguration.ServerConfiguration;

            if (serverConfiguration.UseEmbeddedServer)
            {
                services.AddSingleton<RavenEmbeddedPersistenceLifecycle>();
                services.AddSingleton<IPersistenceLifecycle>(provider => provider.GetRequiredService<RavenEmbeddedPersistenceLifecycle>());
                services.AddSingleton<IRavenDocumentStoreProvider>(provider => provider.GetRequiredService<RavenEmbeddedPersistenceLifecycle>());
            }
            else
            {
                services.AddSingleton<RavenExternalPersistenceLifecycle>();
                services.AddSingleton<IPersistenceLifecycle>(provider => provider.GetRequiredService<RavenExternalPersistenceLifecycle>());
                services.AddSingleton<IRavenDocumentStoreProvider>(provider => provider.GetRequiredService<RavenExternalPersistenceLifecycle>());
            }
        }

        public IPersistenceInstaller CreateInstaller()
        {
            var services = new ServiceCollection();
            ConfigureLifecycle(services);

            services.AddSingleton(databaseConfiguration);

            return new RavenInstaller(services);
        }
    }
}