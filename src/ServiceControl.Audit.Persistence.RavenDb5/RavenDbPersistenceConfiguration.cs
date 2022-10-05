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

            return new RavenDb5Persistence(databaseConfiguration, databaseSetup);
        }

        internal static DatabaseConfiguration GetDatabaseConfiguration(PersistenceSettings settings)
        {
            if (!settings.PersisterSpecificSettings.TryGetValue(DatabaseNameKey, out var databaseName))
            {
                databaseName = "audit";
            }

            ServerConfiguration serverConfiguration;

            if (settings.PersisterSpecificSettings.TryGetValue(DatabasePathKey, out var dbPath))
            {
                if (settings.PersisterSpecificSettings.ContainsKey(ConnectionStringKey))
                {
                    throw new InvalidOperationException($"Both {DatabasePathKey} and {ConnectionStringKey} cant be specified at the same ftime");
                }

                var hostName = settings.PersisterSpecificSettings[HostNameKey];
                var databaseMaintenancePort =
                    int.Parse(settings.PersisterSpecificSettings[DatabaseMaintenancePortKey]);
                var serverUrl = $"http://{hostName}:{databaseMaintenancePort}";

                serverConfiguration = new ServerConfiguration(dbPath, serverUrl);
            }
            else if (settings.PersisterSpecificSettings.TryGetValue(ConnectionStringKey, out var connectionString))
            {
                serverConfiguration = new ServerConfiguration(connectionString);
            }
            else
            {
                throw new InvalidOperationException($"Either {DatabasePathKey} or {ConnectionStringKey} must be specified");
            }

            var expirationProcessTimerInSeconds = GetExpirationProcessTimerInSeconds(settings);

            return new DatabaseConfiguration(
                databaseName,
                expirationProcessTimerInSeconds,
                settings.EnableFullTextSearchOnBodies,
                settings.AuditRetentionPeriod,
                settings.MaxBodySizeToStore,
                serverConfiguration);
        }

        static int GetExpirationProcessTimerInSeconds(PersistenceSettings settings)
        {
            var expirationProcessTimerInSeconds = ExpirationProcessTimerInSecondsDefault;

            if (settings.PersisterSpecificSettings.TryGetValue(ExpirationProcessTimerInSecondsKey, out var expirationProcessTimerInSecondsString))
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

        internal const string DatabaseNameKey = "ServiceControl/Audit/RavenDb5/DatabaseName";
        internal const string DatabasePathKey = "ServiceControl.Audit/DbPath";
        internal const string ConnectionStringKey = "ServiceControl/Audit/RavenDb5/ConnectionString";
        internal const string HostNameKey = "ServiceControl.Audit/HostName";
        internal const string DatabaseMaintenancePortKey = "ServiceControl.Audit/DatabaseMaintenancePort";
        internal const string ExpirationProcessTimerInSecondsKey = "ServiceControl.Audit/ExpirationProcessTimerInSeconds";
    }
}
