namespace ServiceControl.Audit.Persistence.RavenDB
{
    using System;
    using System.Collections.Generic;
    using NServiceBus.Logging;

    public class RavenPersistenceConfiguration : IPersistenceConfiguration
    {
        public const string DatabaseNameKey = "RavenDB/DatabaseName";
        public const string DatabasePathKey = "DbPath";
        public const string ConnectionStringKey = "RavenDB/ConnectionString";
        public const string DatabaseMaintenancePortKey = "DatabaseMaintenancePort";
        public const string ExpirationProcessTimerInSecondsKey = "ExpirationProcessTimerInSeconds";
        public const string LogPathKey = "LogPath";
        public const string RavenDbLogLevelKey = "RavenDBLogLevel";
        public const string MinimumStorageLeftRequiredForIngestionKey = "MinimumStorageLeftRequiredForIngestion";

        public IEnumerable<string> ConfigurationKeys => new[]{
            DatabaseNameKey,
            DatabasePathKey,
            ConnectionStringKey,
            DatabaseMaintenancePortKey,
            ExpirationProcessTimerInSecondsKey,
            LogPathKey,
            RavenDbLogLevelKey,
            MinimumStorageLeftRequiredForIngestionKey
        };

        public string Name => "RavenDB";

        public IPersistence Create(PersistenceSettings settings)
        {
            var databaseConfiguration = GetDatabaseConfiguration(settings);

            return new RavenPersistence(databaseConfiguration);
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

                var logsMode = "Operations";

                if (settings.PersisterSpecificSettings.TryGetValue(RavenDbLogLevelKey, out var ravenDbLogLevel))
                {
                    logsMode = RavenDbLogLevelToLogsModeMapper.Map(ravenDbLogLevel);
                }

                serverConfiguration = new ServerConfiguration(dbPath, serverUrl, logPath, logsMode);
            }
            else if (settings.PersisterSpecificSettings.TryGetValue(ConnectionStringKey, out var connectionString))
            {
                serverConfiguration = new ServerConfiguration(connectionString);
            }
            else
            {
                throw new InvalidOperationException($"Either {DatabasePathKey} or {ConnectionStringKey} must be specified.");
            }

            if (!settings.PersisterSpecificSettings.TryGetValue(MinimumStorageLeftRequiredForIngestionKey, out var minimumStorageLeftRequiredForIngestionKey))
            {
                minimumStorageLeftRequiredForIngestionKey = "5";
            }

            if (!int.TryParse(minimumStorageLeftRequiredForIngestionKey, out var minimumStorageLeftRequiredForIngestion))
            {
                throw new InvalidOperationException($"{MinimumStorageLeftRequiredForIngestionKey} must be an integer.");
            }

            var expirationProcessTimerInSeconds = GetExpirationProcessTimerInSeconds(settings);

            return new DatabaseConfiguration(
                databaseName,
                expirationProcessTimerInSeconds,
                settings.EnableFullTextSearchOnBodies,
                settings.AuditRetentionPeriod,
                settings.MaxBodySizeToStore,
                minimumStorageLeftRequiredForIngestion,
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
                Logger.Error($"ExpirationProcessTimerInSeconds cannot be negative. Defaulting to {ExpirationProcessTimerInSecondsDefault}");
                return ExpirationProcessTimerInSecondsDefault;
            }

            if (expirationProcessTimerInSeconds > TimeSpan.FromHours(3).TotalSeconds)
            {
                Logger.Error($"ExpirationProcessTimerInSeconds cannot be larger than {TimeSpan.FromHours(3).TotalSeconds}. Defaulting to {ExpirationProcessTimerInSecondsDefault}");
                return ExpirationProcessTimerInSecondsDefault;
            }

            return expirationProcessTimerInSeconds;
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(RavenPersistenceConfiguration));

        const int ExpirationProcessTimerInSecondsDefault = 600;
    }
}