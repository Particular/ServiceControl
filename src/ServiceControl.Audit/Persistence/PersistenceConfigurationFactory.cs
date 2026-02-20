namespace ServiceControl.Audit.Persistence
{
    using System;
    using System.IO;
    using Configuration;
    using ServiceControl.Audit.Infrastructure.Settings;

    static class PersistenceConfigurationFactory
    {
        public static IPersistenceConfiguration LoadPersistenceConfiguration(Settings settings)
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

        public static PersistenceSettings BuildPersistenceSettings(this IPersistenceConfiguration persistenceConfiguration, Settings settings)
        {
            var persistenceSettings = new PersistenceSettings(settings.AuditRetentionPeriod, settings.EnableFullTextSearchOnBodies, settings.MaxBodySizeToStore)
            {
                TargetMessageIngestionRate = settings.TargetMessageIngestionRate
            };

            foreach (var key in persistenceConfiguration.ConfigurationKeys)
            {
                var value = SettingsReader.Read<string>(Settings.SettingsRootNamespace, key, null);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    persistenceSettings.PersisterSpecificSettings[key] = value;
                }
            }

            return persistenceSettings;
        }
    }
}