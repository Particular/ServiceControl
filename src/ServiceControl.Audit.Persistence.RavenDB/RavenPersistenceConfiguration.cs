namespace ServiceControl.Audit.Persistence.RavenDB
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using CustomChecks;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using ServiceControl.Infrastructure;
    //
    // class RavenPersistenceSettings
    // {
    //     // public const string DatabaseNameKey = "RavenDB/DatabaseName";
    //     // public const string DatabasePathKey = "DbPath";
    //     // public const string ConnectionStringKey = "RavenDB/ConnectionString";
    //     // public const string ClientCertificatePathKey = "RavenDB/ClientCertificatePath";
    //     // public const string ClientCertificateBase64Key = "RavenDB/ClientCertificateBase64";
    //     // public const string ClientCertificatePasswordKey = "RavenDB/ClientCertificatePassword";
    //     // public const string DatabaseMaintenancePortKey = "DatabaseMaintenancePort";
    //     // public const string ExpirationProcessTimerInSecondsKey = "ExpirationProcessTimerInSeconds";
    //     // public const string LogPathKey = "LogPath";
    //     // public const string RavenDbLogLevelKey = "RavenDBLogLevel";
    //     // public const string MinimumStorageLeftRequiredForIngestionKey = "MinimumStorageLeftRequiredForIngestion";
    //     // public const string BulkInsertCommitTimeoutInSecondsKey = "BulkInsertCommitTimeoutInSeconds";
    //     // public const string DataSpaceRemainingThresholdKey = "DataSpaceRemainingThreshold";
    //
    //     private string DbPath { get; set; } = string.Empty;
    //     public string DatabasePath => DbPath;
    //
    //     private int DatabaseMaintenancePort { get; set; }
    //     public int MaintenancePort => DatabaseMaintenancePort;
    //
    //     public int ExpirationProcessTimerInSeconds { get; set; }
    //     public string LogPath { get; set; } = string.Empty;
    //     public string RavenDBLogLevel { get; set; } = string.Empty;
    //     public long MinimumStorageLeftRequiredForIngestion { get; set; }
    //     public int BulkInsertCommitTimeoutInSeconds { get; set; }
    //     public double DataSpaceRemainingThreshold { get; set; }
    //
    //     public RavenDBSettings RavenDB { get; set; } = new();
    //
    //     class RavenDBSettings
    //     {
    //         public string DatabaseName { get; set; } = string.Empty;
    //         public string ConnectionString { get; set; } = string.Empty;
    //         public string ClientCertificatePath { get; set; } = string.Empty;
    //         public string ClientCertificateBase64 { get; set; } = string.Empty;
    //         public string ClientCertificatePassword { get; set; } = string.Empty;
    //     }
    //
    // }


    public class RavenPersistenceConfiguration : IPersistenceConfiguration
    {
        public const string DatabaseNameKey = "RavenDB:DatabaseName";
        public const string DatabasePathKey = "DbPath";
        public const string ConnectionStringKey = "RavenDB:ConnectionString";
        public const string ClientCertificatePathKey = "RavenDB:ClientCertificatePath";
        public const string ClientCertificateBase64Key = "RavenDB:ClientCertificateBase64";
        public const string ClientCertificatePasswordKey = "RavenDB:ClientCertificatePassword";
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

        public IPersistence Create(PersistenceSettings settings, IConfiguration configuration)
        {
            var databaseConfiguration = GetDatabaseConfiguration(settings, configuration);

            return new RavenPersistence(databaseConfiguration);
        }



        internal static DatabaseConfiguration GetDatabaseConfiguration(
            PersistenceSettings settings,
            IConfiguration configuration
            )
        {
            var databaseName = configuration[DatabaseNameKey];
            if (string.IsNullOrWhiteSpace(databaseName))
            {
                databaseName = "audit";
            }

            ServerConfiguration serverConfiguration;

            var connectionString = configuration[ConnectionStringKey];
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                var databasePath = configuration[DatabasePathKey];
                if (!string.IsNullOrWhiteSpace(databasePath))
                {
                    throw new InvalidOperationException($"{ConnectionStringKey} and {DatabasePathKey} cannot be specified at the same time.");
                }

                serverConfiguration = new ServerConfiguration(connectionString);

                var clientCertificatePath = configuration[ClientCertificatePathKey];
                if (!string.IsNullOrWhiteSpace(clientCertificatePath))
                {
                    serverConfiguration.ClientCertificatePath = clientCertificatePath;
                }

                var clientCertificateBase64 = configuration[ClientCertificateBase64Key];
                if (!string.IsNullOrWhiteSpace(clientCertificateBase64))
                {
                    serverConfiguration.ClientCertificateBase64 = clientCertificateBase64;
                }

                var clientCertificatePassword = configuration[ClientCertificatePasswordKey];
                if (!string.IsNullOrWhiteSpace(clientCertificatePassword))
                {
                    serverConfiguration.ClientCertificatePassword = clientCertificatePassword;
                }
            }
            else
            {
                var dbPath = configuration[DatabasePathKey];
                if (string.IsNullOrWhiteSpace(dbPath))
                {
                    // SC installer always populates DBPath in app.config on installation/change/upgrade so this will only be used when
                    // debugging or if the entry is removed manually. In those circumstances default to the folder containing the exe
                    var assemblyLocation = Assembly.GetExecutingAssembly().Location;
                    dbPath = Path.Combine(Path.GetDirectoryName(assemblyLocation), ".db");
                }

                var databaseMaintenancePortString = configuration[DatabaseMaintenancePortKey];
                if (string.IsNullOrWhiteSpace(databaseMaintenancePortString))
                {
                    throw new InvalidOperationException($"{DatabaseMaintenancePortKey} must be specified when using embedded server.");
                }

                if (!int.TryParse(databaseMaintenancePortString, out var databaseMaintenancePort))
                {
                    throw new InvalidOperationException($"{DatabaseMaintenancePortKey} must be an integer.");
                }

                var serverUrl = $"http://localhost:{databaseMaintenancePort}";

                var logPath = GetLogPath(configuration);

                var logsMode = "Operations";

                var ravenDbLogLevel = configuration[RavenDbLogLevelKey];
                if (!string.IsNullOrWhiteSpace(ravenDbLogLevel))
                {
                    logsMode = RavenDbLogLevelToLogsModeMapper.Map(ravenDbLogLevel, Logger);
                }

                serverConfiguration = new ServerConfiguration(dbPath, serverUrl, logPath, logsMode);
            }

            var dataSpaceRemainingThreshold = CheckFreeDiskSpace.Parse(configuration, Logger);
            var minimumStorageLeftRequiredForIngestion = CheckMinimumStorageRequiredForIngestion.Parse(configuration);

            var expirationProcessTimerInSeconds = GetExpirationProcessTimerInSeconds(configuration);

            var bulkInsertTimeout = TimeSpan.FromSeconds(GetBulkInsertCommitTimeout(configuration));

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

        static int GetExpirationProcessTimerInSeconds(IConfiguration configuration)
        {
            var expirationProcessTimerInSeconds = ExpirationProcessTimerInSecondsDefault;

            var expirationProcessTimerInSecondsString = configuration[ExpirationProcessTimerInSecondsKey];
            if (!string.IsNullOrWhiteSpace(expirationProcessTimerInSecondsString))
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

        static int GetBulkInsertCommitTimeout(IConfiguration configuration)
        {
            var bulkInsertCommitTimeoutInSeconds = BulkInsertCommitTimeoutInSecondsDefault;

            var bulkInsertCommitTimeoutString = configuration[BulkInsertCommitTimeoutInSecondsKey];
            if (!string.IsNullOrWhiteSpace(bulkInsertCommitTimeoutString))
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

        static string GetLogPath(IConfiguration configuration)
        {
            var logPath = configuration[LogPathKey];
            if (string.IsNullOrWhiteSpace(logPath))
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