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
        TimeSpan queryTimeout)
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

        public TimeSpan QueryTimeout { get; } = queryTimeout;
    }
}
