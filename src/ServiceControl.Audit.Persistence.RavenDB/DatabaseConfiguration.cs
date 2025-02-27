namespace ServiceControl.Audit.Persistence.RavenDB
{
    using System;
    using Raven.Client.Documents.Indexes;
    using Sparrow.Json;

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
        SearchEngineType searchEngineType)
    {
        public string Name { get; } = name;

        public int ExpirationProcessTimerInSeconds { get; } = expirationProcessTimerInSeconds;

        public bool EnableFullTextSearch { get; } = enableFullTextSearch;

        public Func<string, BlittableJsonReaderObject, string> FindClrType { get; }

        public ServerConfiguration ServerConfiguration { get; } = serverConfiguration;

        public TimeSpan AuditRetentionPeriod { get; } = auditRetentionPeriod;

        public int MaxBodySizeToStore { get; } = maxBodySizeToStore;

        public int DataSpaceRemainingThreshold { get; } = dataSpaceRemainingThreshold;

        public int MinimumStorageLeftRequiredForIngestion { get; internal set; } = minimumStorageLeftRequiredForIngestion; //Setting for ATT only

        public TimeSpan BulkInsertCommitTimeout { get; } = bulkInsertCommitTimeout;

        public SearchEngineType SearchEngineType { get; } = searchEngineType;
    }
}