namespace ServiceControl.Persistence
{
    using System;
    using ServiceBus.Management.Infrastructure.Settings;

    // Added recently by David

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

        public static PersistenceSettings BuildPersistenceSettings(this IPersistenceConfiguration persistenceConfiguration, Settings settings, bool maintenanceMode = false)
        {
            // TODO: Audit instance passed settings.AuditRetentionPeriod, settings.EnableFullTextSearchOnBodies, settings.MaxBodySizeToStore - are those needed?
            // And then remove settings parameter if not needed (but it probably is for something)
            var persistenceSettings = new PersistenceSettings(
                settings.ErrorRetentionPeriod,
                settings.EventsRetentionPeriod,
                settings.AuditRetentionPeriod,
                settings.ExternalIntegrationsDispatchingBatchSize,
                maintenanceMode
            );

            foreach (var keyPair in settings.PersisterSpecificSettings)
            {
                persistenceSettings.PersisterSpecificSettings[keyPair.Key] = keyPair.Value;
            }

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