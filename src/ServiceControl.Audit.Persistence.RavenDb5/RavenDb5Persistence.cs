﻿namespace ServiceControl.Audit.Persistence.RavenDb
{
    using Microsoft.Extensions.DependencyInjection;
    using Persistence.UnitOfWork;
    using RavenDb5;
    using UnitOfWork;

    class RavenDb5Persistence : IPersistence
    {
        public RavenDb5Persistence(DatabaseConfiguration databaseConfiguration, DatabaseSetup databaseSetup)
        {
            this.databaseConfiguration = databaseConfiguration;
            this.databaseSetup = databaseSetup;
        }

        public IPersistenceLifecycle Configure(IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton(databaseConfiguration);
            serviceCollection.AddSingleton<IRavenDbSessionProvider, RavenDbSessionProvider>();
            serviceCollection.AddSingleton<IAuditDataStore, RavenDbAuditDataStore>();
            serviceCollection.AddSingleton<IAuditIngestionUnitOfWorkFactory, RavenDbAuditIngestionUnitOfWorkFactory>();
            serviceCollection.AddSingleton<IFailedAuditStorage, RavenDbFailedAuditStorage>();

            var lifecycle = CreateLifecycle();

            serviceCollection.AddSingleton<IRavenDbDocumentStoreProvider>(_ => lifecycle);

            return lifecycle;
        }

        public IPersistenceInstaller CreateInstaller() => new RavenDb5Installer(CreateLifecycle(), databaseSetup);

        IRavenDbPersistenceLifecycle CreateLifecycle()
        {
            if (UseEmbeddedInstance(settings))
            {
                var dbPath = settings.PersisterSpecificSettings["ServiceControl.Audit/DbPath"];
                var hostName = settings.PersisterSpecificSettings["ServiceControl.Audit/HostName"];
                var databaseMaintenancePort =
                    int.Parse(settings.PersisterSpecificSettings["ServiceControl.Audit/DatabaseMaintenancePort"]);
                var databaseMaintenanceUrl = $"http://{hostName}:{databaseMaintenancePort}";

                return new RavenDbEmbeddedPersistenceLifecycle(dbPath, databaseMaintenanceUrl, databaseConfiguration);
            }

            var connectionString = settings.PersisterSpecificSettings["ServiceControl/Audit/RavenDb5/ConnectionString"];
            return new RavenDbExternalPersistenceLifecycle(connectionString, databaseConfiguration);
        }

        static bool UseEmbeddedInstance(PersistenceSettings settings)
        {
            if (settings.PersisterSpecificSettings.TryGetValue("ServiceControl/Audit/RavenDb5/UseEmbeddedInstance", out var useEmbeddedInstanceString))
            {
                return bool.Parse(useEmbeddedInstanceString);
            }

            return false;
        }

        readonly DatabaseConfiguration databaseConfiguration;
        readonly DatabaseSetup databaseSetup;
    }
}