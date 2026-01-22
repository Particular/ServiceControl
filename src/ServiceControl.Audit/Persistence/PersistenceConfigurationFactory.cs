namespace ServiceControl.Audit.Persistence
{
    using System;
    using Configuration;
    using ServiceControl.Audit.Infrastructure.Settings;
    using ServiceControl.Audit.Persistence.Sql.PostgreSQL;
    using ServiceControl.Audit.Persistence.Sql.SqlServer;

    static class PersistenceConfigurationFactory
    {
        public static IPersistenceConfiguration LoadPersistenceConfiguration(Settings settings)
        {
            return settings.PersistenceType switch
            {
                "PostgreSQL" => new PostgreSqlAuditPersistenceConfiguration(),
                "SqlServer" => new SqlServerAuditPersistenceConfiguration(),
                _ => throw new Exception($"Unsupported persistence type {settings.PersistenceType}."),
            };
        }

        public static PersistenceSettings BuildPersistenceSettings(this IPersistenceConfiguration persistenceConfiguration, Settings settings)
        {
            var persistenceSettings = new PersistenceSettings(settings.AuditRetentionPeriod, settings.EnableFullTextSearchOnBodies, settings.MaxBodySizeToStore);

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