﻿namespace ServiceControl.Audit.Persistence.RavenDB
{
    using Microsoft.Extensions.DependencyInjection;
    using Persistence.UnitOfWork;
    using RavenDB.CustomChecks;
    using UnitOfWork;

    class RavenPersistence(DatabaseConfiguration databaseConfiguration) : IPersistence
    {
        public void AddPersistence(IServiceCollection services)
        {
            ConfigureLifecycle(services, databaseConfiguration);

            services.AddSingleton<IRavenSessionProvider, RavenSessionProvider>();
            services.AddSingleton<IAuditDataStore, RavenAuditDataStore>();
            services.AddSingleton<IAuditIngestionUnitOfWorkFactory, RavenAuditIngestionUnitOfWorkFactory>();
            services.AddSingleton<IFailedAuditStorage, RavenFailedAuditStorage>();
            services.AddSingleton<CheckMinimumStorageRequiredForAuditIngestion.State>();
        }

        public void AddInstaller(IServiceCollection services) => ConfigureLifecycle(services, databaseConfiguration);

        static void ConfigureLifecycle(IServiceCollection services, DatabaseConfiguration databaseConfiguration)
        {
            services.AddSingleton(databaseConfiguration);
            services.AddHostedService<RavenPersistenceLifecycleHostedService>();

            var serverConfiguration = databaseConfiguration.ServerConfiguration;
            if (serverConfiguration.UseEmbeddedServer)
            {
                // Installer scenarios do not use the host and do not have a lifetime
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