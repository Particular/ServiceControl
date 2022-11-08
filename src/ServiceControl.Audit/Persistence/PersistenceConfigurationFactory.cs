namespace ServiceControl.Audit.Persistence
{
    using System;
    using ServiceControl.Audit.Infrastructure.Settings;

    static class PersistenceConfigurationFactory
    {
        public static IPersistenceConfiguration LoadPersistenceConfiguration()
        {
            var persistenceCustomizationType = SettingsReader<string>.Read("ServiceControl.Audit", "PersistenceType", null);

            try
            {
                var customizationType = Type.GetType(persistenceCustomizationType, true);

                return (IPersistenceConfiguration)Activator.CreateInstance(customizationType);
            }
            catch (Exception e)
            {
                throw new Exception($"Could not load persistence customization type {persistenceCustomizationType}.", e);
            }
        }

        public static PersistenceSettings BuildPersistenceSettings(Settings settings)
        {
            var persistenceSettings = new PersistenceSettings(settings.AuditRetentionPeriod, settings.EnableFullTextSearchOnBodies, settings.MaxBodySizeToStore);

            foreach (var key in ConfigurationKeys)
            {
                var value = SettingsReader<string>.Read("ServiceControl.Audit", key, null);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    persistenceSettings.PersisterSpecificSettings[$"ServiceControl.Audit/{key}"] = value;
                }
            }

            return persistenceSettings;
        }

        static string[] ConfigurationKeys = {
            "DBPath",
            "HostName",
            "DatabaseMaintenancePort",
            "RavenDB35/RunCleanupBundle"
        };
    }
}