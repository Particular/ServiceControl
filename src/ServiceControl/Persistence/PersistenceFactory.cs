namespace ServiceControl.Persistence
{
    using System;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Persistence.Sql.PostgreSQL;
    using ServiceControl.Persistence.Sql.SqlServer;

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
            return settings.PersistenceType switch
            {
                "PostgreSQL" => new PostgreSqlPersistenceConfiguration(),
                "SqlServer" => new SqlServerPersistenceConfiguration(),
                _ => throw new Exception($"Unsupported persistence type {settings.PersistenceType}."),
            };
        }

    }
}
