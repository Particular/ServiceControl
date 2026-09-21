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
        /// <summary>
        /// The manifest names of the two persisters built on EF Core. Anything that is true of both of them and
        /// of neither RavenDB nor a future persister is decided by this list.
        /// </summary>
        public static readonly string[] SqlPersistenceNames = ["SQLServer", "PostgreSQL"];

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
            settings.PersisterSpecificSettings.RetryHistoryDepth = settings.RetryHistoryDepth;

            var persistence = persistenceConfiguration.Create(settings.PersisterSpecificSettings);
            return persistence;
        }

        public static IMigrationSource CreateMigrationSource(Settings settings)
        {
            var persistenceType = settings.MigrationSourcePersistenceType;
            var persistenceConfiguration = CreatePersistenceConfiguration(persistenceType, settings);

            if (persistenceConfiguration is not IMigrationSourceFactory sourceFactory)
            {
                throw new Exception($"The '{persistenceType}' persistence cannot be read as a migration source. Set {Settings.SettingsRootNamespace}/{MigrationSettings.SourcePersistenceTypeKey} to the persistence that holds the data being migrated away from.");
            }

            return sourceFactory.CreateSource(Settings.SettingsRootNamespace);
        }

        public static async Task<IMigrationSource> OpenMigrationSource(Settings settings, CancellationToken cancellationToken = default)
        {
            var source = CreateMigrationSource(settings);
            var opened = false;

            try
            {
                await source.Open(cancellationToken);
                opened = true;
            }
            finally
            {
                if (!opened)
                {
                    await source.DisposeAsync();
                }
            }

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