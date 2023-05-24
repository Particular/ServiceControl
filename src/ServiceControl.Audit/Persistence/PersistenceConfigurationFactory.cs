namespace ServiceControl.Audit.Persistence
{
    using System;
    using ServiceControl.Audit.Infrastructure.Settings;

    static class PersistenceConfigurationFactory
    {
        public static IPersistenceConfiguration LoadPersistenceConfiguration(string persistenceType)
        {
            try
            {
                var foundPersistenceType = PersistenceManifestLibrary.Find(persistenceType);

                var customizationType = Type.GetType(foundPersistenceType, true);

                return (IPersistenceConfiguration)Activator.CreateInstance(customizationType);
            }
            catch (Exception e)
            {
                throw new Exception($"Could not load persistence customization type {persistenceType}.", e);

            }
        }

        public static PersistenceSettings BuildPersistenceSettings(this IPersistenceConfiguration persistenceConfiguration, Settings settings)
        {
            var persistenceSettings = new PersistenceSettings(settings.AuditRetentionPeriod, settings.EnableFullTextSearchOnBodies, settings.MaxBodySizeToStore);

            foreach (var key in persistenceConfiguration.ConfigurationKeys)
            {
                var value = SettingsReader<string>.Read("ServiceControl.Audit", key, null);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    persistenceSettings.PersisterSpecificSettings[key] = value;
                }
            }

            return persistenceSettings;
        }
    }
}