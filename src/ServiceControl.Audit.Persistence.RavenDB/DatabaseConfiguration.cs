namespace ServiceControl.Audit.Persistence.RavenDB
{
    using System;

    public class DatabaseConfiguration(
        string name,
        int expirationProcessTimerInSeconds,
        bool enableFullTextSearch,
        TimeSpan auditRetentionPeriod,
        int maxBodySizeToStore,
        int dataSpaceRemainingThreshold,
        int minimumStorageLeftRequiredForIngestion,
        ServerConfiguration serverConfiguration,
        TimeSpan bulkInsertCommitTimeout,
        bool enableAuditRetentionBuckets = false,
        TimeSpan? auditRetentionBucketDuration = null)
    {
        public string Name { get; } = name;

        public int ExpirationProcessTimerInSeconds { get; } = expirationProcessTimerInSeconds;

        public bool EnableFullTextSearch { get; } = enableFullTextSearch;

        public ServerConfiguration ServerConfiguration { get; } = serverConfiguration;

        public TimeSpan AuditRetentionPeriod { get; } = auditRetentionPeriod;

        public int MaxBodySizeToStore { get; } = maxBodySizeToStore;

        public int DataSpaceRemainingThreshold { get; } = dataSpaceRemainingThreshold;

        public int MinimumStorageLeftRequiredForIngestion { get; internal set; } = minimumStorageLeftRequiredForIngestion; //Setting for ATT only

        public TimeSpan BulkInsertCommitTimeout { get; } = bulkInsertCommitTimeout;

        /// <summary>
        /// Opt-in retention bucket mode. When enabled, ProcessedMessage and SagaSnapshot documents are
        /// stored in fixed UTC time buckets with dedicated collections and dedicated static indexes, and
        /// retention is enforced by deleting whole buckets instead of relying on Raven's document expiry.
        /// Disabled by default, in which case the existing Raven document-expiry behavior is unchanged.
        /// </summary>
        public bool EnableAuditRetentionBuckets { get; } = enableAuditRetentionBuckets;

        /// <summary>
        /// Default UTC bucket duration used by bucket mode when no explicit value is configured.
        /// </summary>
        public static readonly TimeSpan DefaultAuditRetentionBucketDuration = TimeSpan.FromHours(1);

        /// <summary>
        /// Configurable UTC bucket duration used by bucket mode (see
        /// RavenPersistenceConfiguration.AuditRetentionBucketDurationKey). Defaults to one hour. Must be a
        /// whole number of hours between one hour and 31 days so the "yyyyMMdd_HH" bucket key derived from
        /// the bucket start stays collision-free. The persisted catalog records the value and rejects a
        /// different duration for an existing database, so changing it requires a fresh database.
        /// </summary>
        public TimeSpan AuditRetentionBucketDuration { get; } = auditRetentionBucketDuration ?? DefaultAuditRetentionBucketDuration;
    }
}
