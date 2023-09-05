namespace ServiceControl.Persistence
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Web.Hosting;
    using ServiceBus.Management.Infrastructure.Settings;

    static class PersistenceFactory
    {
        public static IPersistence Create(Settings settings, bool maintenanceMode = false)
        {
            var persistenceConfiguration = CreatePersistenceConfiguration(settings.PersistenceType);

            //HINT: This is false when executed from acceptance tests
            if (settings.PersisterSpecificSettings == null)
            {
                (bool, object) TryRead(string name, Type type)
                {
                    var exists = SettingsReader.TryRead(name, type: type, out object value);
                    return (exists, value);
                };

                settings.PersisterSpecificSettings = persistenceConfiguration.CreateSettings(TryRead);
            }

            settings.PersisterSpecificSettings.MaintenanceMode = maintenanceMode;
            settings.PersisterSpecificSettings.DatabasePath = BuildDataBasePath(settings);

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

        static string BuildDataBasePath(Settings settings)
        {
            var host = settings.Hostname;
            if (host == "*")
            {
                host = "%";
            }

            var dbFolder = $"{host}-{settings.Port}";

            if (!string.IsNullOrEmpty(settings.VirtualDirectory))
            {
                dbFolder += $"-{SanitiseFolderName(settings.VirtualDirectory)}";
            }

            var defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Particular", "ServiceControl", dbFolder);

            return SettingsReader.Read("DbPath", defaultPath);
        }

        static string SanitiseFolderName(string folderName)
        {
            return Path.GetInvalidPathChars().Aggregate(folderName, (current, c) => current.Replace(c, '-'));
        }

    }
}