namespace ServiceControl.Audit.Persistence.RavenDb
{
    using System;
    using NServiceBus.Logging;
    using ServiceControl.Audit.Persistence.RavenDb5;

    public class RavenDbPersistenceConfiguration : IPersistenceConfiguration
    {
        public IPersistence Create(PersistenceSettings settings)
        {
            var databaseConfiguration = GetDatabaseConfiguration(settings);
            var databaseSetup = new DatabaseSetup(databaseConfiguration);

            return new RavenDb5Persistence(databaseConfiguration, databaseSetup, settings);
        }
        static DatabaseConfiguration GetDatabaseConfiguration(PersistenceSettings settings)
        {
            if (!settings.PersisterSpecificSettings.TryGetValue("ServiceControl/Audit/RavenDb5/DatabaseName", out var databaseName))
            {
                databaseName = "audit";
            }

            var expirationProcessTimerInSeconds = GetExpirationProcessTimerInSeconds(settings);

            return new DatabaseConfiguration(databaseName, expirationProcessTimerInSeconds, settings.EnableFullTextSearchOnBodies);
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
