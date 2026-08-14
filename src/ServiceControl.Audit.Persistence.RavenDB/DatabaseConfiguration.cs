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
        bool enableAuditRetentionBuckets = false)
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
        /// Fixed UTC bucket duration used by bucket mode. The bucket key format and the persisted catalog
        /// contract depend on this value, so it must not be changed for an existing database.
        /// </summary>
        public static readonly TimeSpan AuditRetentionBucketDuration = TimeSpan.FromHours(1);
    }
}
