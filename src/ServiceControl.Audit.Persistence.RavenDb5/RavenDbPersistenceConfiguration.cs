namespace ServiceControl.Audit.Persistence.RavenDb
{
    using System;
    using NServiceBus.Logging;
    using ServiceControl.Audit.Persistence.RavenDb5;

    public class RavenDbPersistenceConfiguration : IPersistenceConfiguration
    {
        public IPersistence Create(PersistenceSettings settings)
        {
            var dataBaseConfiguration = GetDatabaseConfiguration(settings);
            var expirationProcessTimerInSeconds = GetExpirationProcessTimerInSeconds(settings);
            var databaseSetup = new DatabaseSetup(expirationProcessTimerInSeconds, settings.EnableFullTextSearchOnBodies, dataBaseConfiguration);

            return new RavenDb5Persistence(dataBaseConfiguration, databaseSetup, settings);
        }
        static AuditDatabaseConfiguration GetDatabaseConfiguration(PersistenceSettings settings)
        {
            if (!settings.PersisterSpecificSettings.TryGetValue("ServiceControl/Audit/RavenDb5/DatabaseName", out var databaseName))
            {
                databaseName = "audit";
            }

            return new AuditDatabaseConfiguration(databaseName);
        }

        static int GetExpirationProcessTimerInSeconds(PersistenceSettings settings)
        {
            var expirationProcessTimerInSeconds = ExpirationProcessTimerInSecondsDefault;

            if (settings.PersisterSpecificSettings.TryGetValue("ServiceControl.Audit/ExpirationProcessTimerInSeconds", out var expirationProcessTimerInSecondsString))
            {
                expirationProcessTimerInSeconds = int.Parse(expirationProcessTimerInSecondsString);
            }

            if (expirationProcessTimerInSeconds < 0)
            {
                logger.Error($"ExpirationProcessTimerInSeconds cannot be negative. Defaulting to {ExpirationProcessTimerInSecondsDefault}");
                return ExpirationProcessTimerInSecondsDefault;
            }

            if (expirationProcessTimerInSeconds > TimeSpan.FromHours(3).TotalSeconds)
            {
                logger.Error($"ExpirationProcessTimerInSeconds cannot be larger than {TimeSpan.FromHours(3).TotalSeconds}. Defaulting to {ExpirationProcessTimerInSecondsDefault}");
                return ExpirationProcessTimerInSecondsDefault;
            }

            return expirationProcessTimerInSeconds;
        }

        static ILog logger = LogManager.GetLogger(typeof(RavenDbPersistenceConfiguration));

        const int ExpirationProcessTimerInSecondsDefault = 600;
    }
}
