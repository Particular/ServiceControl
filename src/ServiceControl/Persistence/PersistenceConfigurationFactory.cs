namespace ServiceControl.Persistence
{
    using System;
    using ServiceBus.Management.Infrastructure.Settings;

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

#pragma warning disable IDE0060 // Remove unused parameter
        public static PersistenceSettings BuildPersistenceSettings(this IPersistenceConfiguration persistenceConfiguration, Settings settings)
#pragma warning restore IDE0060 // Remove unused parameter
        {
            // TODO: Audit instance passed settings.AuditRetentionPeriod, settings.EnableFullTextSearchOnBodies, settings.MaxBodySizeToStore - are those needed?
            // And then remove settings parameter if not needed (but it probably is for something)
            var persistenceSettings = new PersistenceSettings();

            foreach (var key in persistenceConfiguration.ConfigurationKeys)
            {
                var value = SettingsReader<string>.Read("ServiceControl", key, null);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    persistenceSettings.PersisterSpecificSettings[key] = value;
                }
            }

            return persistenceSettings;
        }
    }
}