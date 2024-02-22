namespace ServiceControl.Persistence
{
    using System;
    using System.IO;
    using System.Reflection;
    using Configuration;
    using ServiceBus.Management.Infrastructure.Settings;

    static class PersistenceFactory
    {
        public static IPersistence Create(Settings settings, bool maintenanceMode = false)
        {
            var persistenceConfiguration = CreatePersistenceConfiguration(settings.PersistenceType);

            //HINT: This is false when executed from acceptance tests
            settings.PersisterSpecificSettings ??= persistenceConfiguration.CreateSettings(Settings.SettingsRootNamespace);

            settings.PersisterSpecificSettings.MaintenanceMode = maintenanceMode;
            settings.PersisterSpecificSettings.DatabasePath = BuildDataBasePath();

            var persistence = persistenceConfiguration.Create(settings.PersisterSpecificSettings);
            return persistence;
        }

        static IPersistenceConfiguration CreatePersistenceConfiguration(string persistenceType)
        {
            try
            {
                var foundPersistenceType = PersistenceManifestLibrary.Find(persistenceType);
                var customizationType = Type.GetType(foundPersistenceType, true);
                var persistenceConfiguration = (IPersistenceConfiguration)Activator.CreateInstance(customizationType);
                return persistenceConfiguration;
            }
            catch (Exception e)
            {
                throw new Exception($"Could not load persistence customization type {persistenceType}.", e);
            }
        }

        static string BuildDataBasePath()
        {
            // SC installer always populates DBPath in app.config on installation/change/upgrade so this will only be used when
            // debugging or if the entry is removed manually. In those circumstances default to the folder containing the exe
            var assemblyLocation = Assembly.GetExecutingAssembly().Location;
            var defaultPath = Path.Combine(Path.GetDirectoryName(assemblyLocation), ".db");

            return SettingsReader.Read(Settings.SettingsRootNamespace, "DbPath", defaultPath);
        }
    }
}