namespace ServiceControl.Audit.Persistence.RavenDB
{
    using System;
    using System.Configuration;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using CustomChecks;
    using Microsoft.Extensions.Logging;
    using ServiceControl.Infrastructure;

    public class RavenPersistenceConfiguration : IPersistenceConfiguration
    {
        public const string DatabaseNameKey = "RavenDB/DatabaseName";
        public const string DatabasePathKey = "DbPath";
        public const string ConnectionStringKey = "RavenDB/ConnectionString";
        public const string ClientCertificatePathKey = "RavenDB/ClientCertificatePath";
        public const string ClientCertificateBase64Key = "RavenDB/ClientCertificateBase64";
        public const string ClientCertificatePasswordKey = "RavenDB/ClientCertificatePassword";
        public const string DatabaseMaintenancePortKey = "DatabaseMaintenancePort";
        public const string ExpirationProcessTimerInSecondsKey = "ExpirationProcessTimerInSeconds";
        public const string LogPathKey = "LogPath";
        public const string RavenDbLogLevelKey = "RavenDBLogLevel";
        public const string MinimumStorageLeftRequiredForIngestionKey = "MinimumStorageLeftRequiredForIngestion";
        public const string BulkInsertCommitTimeoutInSecondsKey = "BulkInsertCommitTimeoutInSeconds";
        public const string DataSpaceRemainingThresholdKey = "DataSpaceRemainingThreshold";

        public IEnumerable<string> ConfigurationKeys => new[]{
            DatabaseNameKey,
            DatabasePathKey,
            ConnectionStringKey,
            ClientCertificatePathKey,
            ClientCertificateBase64Key,
            ClientCertificatePasswordKey,
            DatabaseMaintenancePortKey,
            ExpirationProcessTimerInSecondsKey,
            LogPathKey,
            RavenDbLogLevelKey,
            DataSpaceRemainingThresholdKey,
            MinimumStorageLeftRequiredForIngestionKey,
            BulkInsertCommitTimeoutInSecondsKey
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

            if (settings.PersisterSpecificSettings.TryGetValue(ConnectionStringKey, out var connectionString))
            {
                if (settings.PersisterSpecificSettings.ContainsKey(DatabasePathKey))
                {
                    throw new InvalidOperationException($"{ConnectionStringKey} and {DatabasePathKey} cannot be specified at the same time.");
                }

                serverConfiguration = new ServerConfiguration(connectionString);

                if (settings.PersisterSpecificSettings.TryGetValue(ClientCertificatePathKey, out var clientCertificatePath))
                {
                    serverConfiguration.ClientCertificatePath = clientCertificatePath;
                }
                if (settings.PersisterSpecificSettings.TryGetValue(ClientCertificateBase64Key, out var clientCertificateBase64))
                {
                    serverConfiguration.ClientCertificateBase64 = clientCertificateBase64;
                }
                if (settings.PersisterSpecificSettings.TryGetValue(ClientCertificatePasswordKey, out var clientCertificatePassword))
                {
                    serverConfiguration.ClientCertificatePassword = clientCertificatePassword;
                }
            }
            else
            {
                if (!settings.PersisterSpecificSettings.TryGetValue(DatabasePathKey, out var dbPath))
                {
                    // SC installer always populates DBPath in app.config on installation/change/upgrade so this will only be used when
                    // debugging or if the entry is removed manually. In those circumstances default to the folder containing the exe
                    var assemblyLocation = Assembly.GetExecutingAssembly().Location;
                    dbPath = Path.Combine(Path.GetDirectoryName(assemblyLocation), ".db");
                }

                if (!settings.PersisterSpecificSettings.TryGetValue(DatabaseMaintenancePortKey, out var databaseMaintenancePortString))
                {
                    throw new InvalidOperationException($"{DatabaseMaintenancePortKey} must be specified when using embedded server.");
                }

                if (!int.TryParse(databaseMaintenancePortString, out var databaseMaintenancePort))
                {
                    throw new InvalidOperationException($"{DatabaseMaintenancePortKey} must be an integer.");
                }


                string host = "localhost";
                if (!string.IsNullOrEmpty(settings.Hostname))
                {
                    // Map '*' to '+' for Raven wildcard (bind all interfaces). Accept '+' as-is.
                    host = settings.Hostname == "*" ? "+" : settings.Hostname;
                }
                var serverUrl = $"http://{host}:{databaseMaintenancePort}";

                var logPath = GetLogPath(settings);

                var logsMode = "Operations";

                if (settings.PersisterSpecificSettings.TryGetValue(RavenDbLogLevelKey, out var ravenDbLogLevel))
                {
                    logsMode = RavenDbLogLevelToLogsModeMapper.Map(ravenDbLogLevel, Logger);
                }

                serverConfiguration = new ServerConfiguration(dbPath, serverUrl, logPath, logsMode);
            }

            var dataSpaceRemainingThreshold = CheckFreeDiskSpace.Parse(settings.PersisterSpecificSettings, Logger);
            var minimumStorageLeftRequiredForIngestion = CheckMinimumStorageRequiredForIngestion.Parse(settings.PersisterSpecificSettings);

            var expirationProcessTimerInSeconds = GetExpirationProcessTimerInSeconds(settings);

            var bulkInsertTimeout = TimeSpan.FromSeconds(GetBulkInsertCommitTimeout(settings));

            return new DatabaseConfiguration(
                databaseName,
                expirationProcessTimerInSeconds,
                settings.EnableFullTextSearchOnBodies,
                settings.AuditRetentionPeriod,
                settings.MaxBodySizeToStore,
                dataSpaceRemainingThreshold,
                minimumStorageLeftRequiredForIngestion,
                serverConfiguration,
                bulkInsertTimeout);
        }

        static int GetExpirationProcessTimerInSeconds(PersistenceSettings settings)
        {
            var expirationProcessTimerInSeconds = ExpirationProcessTimerInSecondsDefault;

            if (settings.PersisterSpecificSettings.TryGetValue(ExpirationProcessTimerInSecondsKey, out var expirationProcessTimerInSecondsString))
            {
                expirationProcessTimerInSeconds = int.Parse(expirationProcessTimerInSecondsString);
            }

            var maxExpirationProcessTimerInSeconds = TimeSpan.FromHours(3).TotalSeconds;

            if (expirationProcessTimerInSeconds < 0)
            {
                Logger.LogError("ExpirationProcessTimerInSeconds cannot be negative. Defaulting to {ExpirationProcessTimerInSecondsDefault}", ExpirationProcessTimerInSecondsDefault);
                return ExpirationProcessTimerInSecondsDefault;
            }

            if (expirationProcessTimerInSeconds > maxExpirationProcessTimerInSeconds)
            {
                Logger.LogError("ExpirationProcessTimerInSeconds cannot be larger than {MaxExpirationProcessTimerInSeconds}. Defaulting to {ExpirationProcessTimerInSecondsDefault}", maxExpirationProcessTimerInSeconds, ExpirationProcessTimerInSecondsDefault);
                return ExpirationProcessTimerInSecondsDefault;
            }

            return expirationProcessTimerInSeconds;
        }

        static int GetBulkInsertCommitTimeout(PersistenceSettings settings)
        {
            var bulkInsertCommitTimeoutInSeconds = BulkInsertCommitTimeoutInSecondsDefault;

            if (settings.PersisterSpecificSettings.TryGetValue(BulkInsertCommitTimeoutInSecondsKey, out var bulkInsertCommitTimeoutString))
            {
                bulkInsertCommitTimeoutInSeconds = int.Parse(bulkInsertCommitTimeoutString);
            }

            var maxBulkInsertCommitTimeoutInSeconds = TimeSpan.FromHours(1).TotalSeconds;

            if (bulkInsertCommitTimeoutInSeconds < 0)
            {
                Logger.LogError("BulkInsertCommitTimeout cannot be negative. Defaulting to {BulkInsertCommitTimeoutInSecondsDefault}", BulkInsertCommitTimeoutInSecondsDefault);
                return BulkInsertCommitTimeoutInSecondsDefault;
            }

            if (bulkInsertCommitTimeoutInSeconds > maxBulkInsertCommitTimeoutInSeconds)
            {
                Logger.LogError("BulkInsertCommitTimeout cannot be larger than {MaxBulkInsertCommitTimeoutInSeconds}. Defaulting to {BulkInsertCommitTimeoutInSecondsDefault}", maxBulkInsertCommitTimeoutInSeconds, BulkInsertCommitTimeoutInSecondsDefault);
                return BulkInsertCommitTimeoutInSecondsDefault;
            }

            return bulkInsertCommitTimeoutInSeconds;
        }

        static string GetLogPath(PersistenceSettings settings)
        {
            if (!settings.PersisterSpecificSettings.TryGetValue(LogPathKey, out var logPath))
            {
                // SC installer always populates LogPath in app.config on installation/change/upgrade so this will only be used when
                // debugging or if the entry is removed manually. In those circumstances default to the folder containing the exe
                var assemblyLocation = Assembly.GetExecutingAssembly().Location;
                logPath = Path.Combine(Path.GetDirectoryName(assemblyLocation), ".logs");
            }

            return logPath;
        }

        const int ExpirationProcessTimerInSecondsDefault = 600;
        const int BulkInsertCommitTimeoutInSecondsDefault = 60;
        static readonly ILogger<RavenPersistenceConfiguration> Logger = LoggerUtil.CreateStaticLogger<RavenPersistenceConfiguration>();
    }
}
