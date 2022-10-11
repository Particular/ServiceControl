namespace ServiceControl.Audit.Persistence
{
    using System;
    using System.Configuration;
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

            //hardcode for now
            foreach (var key in ConfigurationManager.AppSettings.AllKeys)
            {
                persistenceSettings.PersisterSpecificSettings[key] = ConfigurationManager.AppSettings[key];
            }

            return persistenceSettings;
        }
    }
}