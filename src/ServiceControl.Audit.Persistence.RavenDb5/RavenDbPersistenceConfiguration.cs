namespace ServiceControl.Audit.Persistence.RavenDb
{
    using System;
    using System.Collections.Generic;
    using NServiceBus.Logging;

    public class RavenDbPersistenceConfiguration : IPersistenceConfiguration
    {
        public const string DatabaseNameKey = "RavenDB5/DatabaseName";
        public const string DatabasePathKey = "DbPath";
        public const string ConnectionStringKey = "RavenDB5/ConnectionString";
        public const string DatabaseMaintenancePortKey = "DatabaseMaintenancePort";
        public const string ExpirationProcessTimerInSecondsKey = "ExpirationProcessTimerInSeconds";
        public const string LogPathKey = "LogPath";

        public IEnumerable<string> ConfigurationKeys => new string[]{
            DatabaseNameKey,
            DatabasePathKey,
            ConnectionStringKey,
            DatabaseMaintenancePortKey,
            ExpirationProcessTimerInSecondsKey
        };

        public string Name => "RavenDB5";

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
                    throw new InvalidOperationException($"{DatabasePathKey} and {ConnectionStringKey} cannot be specified at the same time.");
                }

                if (!settings.PersisterSpecificSettings.TryGetValue(DatabaseMaintenancePortKey, out var databaseMaintenancePortString))
                {
                    throw new InvalidOperationException($"{DatabaseMaintenancePortKey} must be specified when using embedded server.");
                }

                if (!int.TryParse(databaseMaintenancePortString, out var databaseMaintenancePort))
                {
                    throw new InvalidOperationException($"{DatabaseMaintenancePortKey} must be an integer.");
                }

                var serverUrl = $"http://localhost:{databaseMaintenancePort}";

                if (!settings.PersisterSpecificSettings.TryGetValue(LogPathKey, out var logPath))
                {
                    throw new InvalidOperationException($"{LogPathKey}  must be specified when using embedded server.");
                }

                serverConfiguration = new ServerConfiguration(dbPath, serverUrl, logPath);
            }
            else if (settings.PersisterSpecificSettings.TryGetValue(ConnectionStringKey, out var connectionString))
            {
                serverConfiguration = new ServerConfiguration(connectionString);
            }
            else
            {
                throw new InvalidOperationException($"Either {DatabasePathKey} or {ConnectionStringKey} must be specified.");
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
    }
}
