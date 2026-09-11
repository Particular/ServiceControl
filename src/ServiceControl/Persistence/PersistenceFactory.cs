namespace ServiceControl.Persistence
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Persistence.DataMigration;

    static class PersistenceFactory
    {
        public static IPersistence Create(Settings settings, bool maintenanceMode = false)
        {
            var persistenceConfiguration = CreatePersistenceConfiguration(settings.PersistenceType, settings);

            if (maintenanceMode && !persistenceConfiguration.SupportsMaintenanceMode)
            {
                throw new Exception($"Maintenance mode is not supported by the {settings.PersistenceType} persister. It is only available on RavenDB, where it starts the embedded database so RavenDB Studio can be used.");
            }

            //HINT: This is false when executed from acceptance tests
            settings.PersisterSpecificSettings ??= persistenceConfiguration.CreateSettings(Settings.SettingsRootNamespace);
            settings.PersisterSpecificSettings.MaintenanceMode = maintenanceMode;
            settings.PersisterSpecificSettings.RunRetentionSweep = !settings.ErrorIngestionOnly;

            var persistence = persistenceConfiguration.Create(settings.PersisterSpecificSettings);
            return persistence;
        }

        public static IMigrationSource CreateMigrationSource(Settings settings)
        {
            var persistenceType = settings.MigrationSourcePersistenceType;
            var persistenceConfiguration = CreatePersistenceConfiguration(persistenceType, settings);

            if (persistenceConfiguration is not IMigrationSourceFactory sourceFactory)
            {
                throw new Exception($"The '{persistenceType}' persistence cannot be read as a migration source. Set ServiceControl/Migration/SourcePersistenceType to the persistence that holds the data being migrated away from.");
            }

            // Not PersisterSpecificSettings: it is null here, and a host that has populated it put the
            // target's connection string in it.
            return sourceFactory.CreateSource(persistenceConfiguration.CreateSettings(Settings.SettingsRootNamespace));
        }

        public static async Task<IMigrationSource> OpenMigrationSource(Settings settings, CancellationToken cancellationToken = default)
        {
            var source = CreateMigrationSource(settings);
            await source.Open(cancellationToken);

            return source;
        }

        static IPersistenceConfiguration CreatePersistenceConfiguration(string persistenceType, Settings settings)
        {
            var persistenceManifest = PersistenceManifestLibrary.Find(persistenceType)
                ?? throw new Exception($"There is no persistence named '{persistenceType}'. Available: {string.Join(", ", PersistenceManifestLibrary.PersistenceManifests.Where(m => m.IsSupported).Select(m => m.Name))}.");

            if (persistenceManifest.TypeName is null)
            {
                throw new Exception($"The '{persistenceManifest.DisplayName}' persistence no longer ships an assembly and cannot be loaded.");
            }

            try
            {
                var assemblyPath = Path.Combine(persistenceManifest.Location, $"{persistenceManifest.AssemblyName}.dll");
                var loadContext = settings.AssemblyLoadContextResolver(assemblyPath);
                var customizationType = Type.GetType(persistenceManifest.TypeName, loadContext.LoadFromAssemblyName, null, true);

                return (IPersistenceConfiguration)Activator.CreateInstance(customizationType);
            }
            catch (Exception e)
            {
                throw new Exception($"Could not load persistence customization type {persistenceType}.", e);
            }
        }
    }
}