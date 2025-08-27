namespace ServiceControl.Audit.Persistence.PostgreSQL
{
    using System;
    using System.Collections.Generic;
    using ServiceControl.Audit.Persistence;

    public class PostgreSQLPersistenceConfiguration : IPersistenceConfiguration
    {
        public string Name => "PostgreSQL";

        public IEnumerable<string> ConfigurationKeys => ["PostgreSql/ConnectionString", "PostgreSql/DatabaseName"];

        const int ExpirationProcessTimerInSecondsDefault = 600;

        public IPersistence Create(PersistenceSettings settings)
        {
            if (!settings.PersisterSpecificSettings.TryGetValue("PostgreSql/ConnectionString", out var connectionString))
            {
                throw new Exception("PostgreSql/ConnectionString is not configured.");
            }

            if (!settings.PersisterSpecificSettings.TryGetValue("PostgreSql/DatabaseName", out var databaseName))
            {
                databaseName = "servicecontrol-audit";
            }

            return new PostgreSQLPersistence(new DatabaseConfiguration(
                databaseName,
                ExpirationProcessTimerInSecondsDefault,
                settings.AuditRetentionPeriod,
                settings.MaxBodySizeToStore,
                connectionString));
        }
    }
}
