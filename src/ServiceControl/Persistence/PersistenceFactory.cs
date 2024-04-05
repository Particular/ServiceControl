namespace ServiceControl.Persistence
{
    using System;
    using System.IO;
    using ServiceBus.Management.Infrastructure.Settings;

    static class PersistenceFactory
    {
        public static IPersistence Create(Settings settings, bool maintenanceMode = false)
        {
            var persistenceConfiguration = CreatePersistenceConfiguration(settings);

            //HINT: This is false when executed from acceptance tests
            settings.PersisterSpecificSettings ??= persistenceConfiguration.CreateSettings(Settings.SettingsRootNamespace);
            settings.PersisterSpecificSettings.MaintenanceMode = maintenanceMode;

            var persistence = persistenceConfiguration.Create(settings.PersisterSpecificSettings);
            return persistence;
        }

        static IPersistenceConfiguration CreatePersistenceConfiguration(Settings settings)
        {
            try
            {
                var persistenceManifest = PersistenceManifestLibrary.Find(settings.PersistenceType);
                var assemblyPath = Path.Combine(persistenceManifest.Location, $"{persistenceManifest.AssemblyName}.dll");
                var loadContext = settings.AssemblyLoadContextResolver(assemblyPath);
                var customizationType = Type.GetType(persistenceManifest.TypeName, loadContext.LoadFromAssemblyName, null, true);

                return (IPersistenceConfiguration)Activator.CreateInstance(customizationType);
            }
            catch (Exception e)
            {
                throw new Exception($"Could not load persistence customization type {settings.PersistenceType}.", e);
            }
        }
    }
}