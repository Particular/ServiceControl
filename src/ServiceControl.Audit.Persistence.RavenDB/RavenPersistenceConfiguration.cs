namespace ServiceControl.Audit.Persistence.RavenDB
{
    using System;
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
        public const string EnableAuditRetentionBucketsKey = "RavenDB/EnableAuditRetentionBuckets";
        public const string AuditRetentionBucketDurationKey = "RavenDB/AuditRetentionBucketDuration";

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
            BulkInsertCommitTimeoutInSecondsKey,
            EnableAuditRetentionBucketsKey,
            AuditRetentionBucketDurationKey
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

                var serverUrl = $"http://localhost:{databaseMaintenancePort}";

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

            var enableAuditRetentionBuckets = GetEnableAuditRetentionBuckets(settings);

            var auditRetentionBucketDuration = GetAuditRetentionBucketDuration(settings);

            if (enableAuditRetentionBuckets)
            {
                if (auditRetentionBucketDuration > settings.AuditRetentionPeriod)
                {
                    Logger.LogWarning("{AuditRetentionBucketDurationKey} ({BucketDuration}) is longer than the audit retention period ({AuditRetentionPeriod}). Retention deletes whole buckets, so the effective retention will overshoot the configured period", AuditRetentionBucketDurationKey, auditRetentionBucketDuration, settings.AuditRetentionPeriod);
                }

                // Retention keeps every bucket whose End + retention is still in the future, so the
                // expected number of active buckets scales with retention / duration. Every read fans
                // out one query per active bucket, which makes the bucket count the dominant cost
                // driver of bucket mode; log it so operators can correlate query load with sizing.
                var expectedActiveBuckets = (int)Math.Ceiling(settings.AuditRetentionPeriod / auditRetentionBucketDuration) + 1;
                Logger.LogInformation("Audit retention buckets are enabled with a bucket duration of {BucketDuration}; expect up to {ExpectedActiveBuckets} active buckets for the {AuditRetentionPeriod} audit retention period", auditRetentionBucketDuration, expectedActiveBuckets, settings.AuditRetentionPeriod);
            }

            var expirationProcessTimerInSeconds = GetExpirationProcessTimerInSeconds(settings, enableAuditRetentionBuckets);

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
                bulkInsertTimeout,
                enableAuditRetentionBuckets,
                auditRetentionBucketDuration);
        }

        static bool GetEnableAuditRetentionBuckets(PersistenceSettings settings)
        {
            if (!settings.PersisterSpecificSettings.TryGetValue(EnableAuditRetentionBucketsKey, out var value))
            {
                return false;
            }

            if (!bool.TryParse(value, out var enabled))
            {
                throw new InvalidOperationException($"{EnableAuditRetentionBucketsKey} must be a boolean value.");
            }

            return enabled;
        }

        static TimeSpan GetAuditRetentionBucketDuration(PersistenceSettings settings)
        {
            if (!settings.PersisterSpecificSettings.TryGetValue(AuditRetentionBucketDurationKey, out var value))
            {
                return DatabaseConfiguration.DefaultAuditRetentionBucketDuration;
            }

            if (!TimeSpan.TryParse(value, out var duration))
            {
                throw new InvalidOperationException($"{AuditRetentionBucketDurationKey} must be a TimeSpan value, e.g. 01:00:00 for one hour or 1.00:00:00 for one day.");
            }

            if (duration < TimeSpan.FromHours(1))
            {
                throw new InvalidOperationException($"{AuditRetentionBucketDurationKey} must be at least one hour.");
            }

            if (duration > TimeSpan.FromDays(31))
            {
                throw new InvalidOperationException($"{AuditRetentionBucketDurationKey} must not exceed 31 days.");
            }

            // Bucket keys only carry the hour of the bucket start ("yyyyMMdd_HH"), so a duration that
            // is not a whole number of hours could map two buckets onto the same key.
            if (duration.Ticks % TimeSpan.TicksPerHour != 0)
            {
                throw new InvalidOperationException($"{AuditRetentionBucketDurationKey} must be a whole number of hours.");
            }

            return duration;
        }

        static int GetExpirationProcessTimerInSeconds(PersistenceSettings settings, bool enableAuditRetentionBuckets)
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

            if (expirationProcessTimerInSeconds == 0 && enableAuditRetentionBuckets)
            {
                // The bucket cleanup loop is driven by a PeriodicTimer, which requires a positive period.
                throw new InvalidOperationException(
                    $"{ExpirationProcessTimerInSecondsKey} must be greater than zero when {EnableAuditRetentionBucketsKey} is enabled. " +
                    "The audit retention bucket cleanup timer requires a positive period.");
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
