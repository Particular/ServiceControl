﻿namespace ServiceControl.Audit.Persistence.RavenDB
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
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
                // Installer scenarios do not use the host and do not have a lifetime
                services.AddSingleton(provider => new RavenEmbeddedPersistenceLifecycle(provider.GetRequiredService<DatabaseConfiguration>(), provider.GetService<IHostApplicationLifetime>()));
                services.AddSingleton<IPersistenceLifecycle>(provider => provider.GetRequiredService<RavenEmbeddedPersistenceLifecycle>());
                services.AddSingleton<IRavenDocumentStoreProvider>(provider => provider.GetRequiredService<RavenEmbeddedPersistenceLifecycle>());
                return;
            }

            services.AddSingleton<RavenExternalPersistenceLifecycle>();
            services.AddSingleton<IPersistenceLifecycle>(provider => provider.GetRequiredService<RavenExternalPersistenceLifecycle>());
            services.AddSingleton<IRavenDocumentStoreProvider>(provider => provider.GetRequiredService<RavenExternalPersistenceLifecycle>());
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