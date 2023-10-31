namespace ServiceControl.Audit.Persistence.RavenDB
{
    using System;
    using Sparrow.Json;

    public class DatabaseConfiguration
    {
        public DatabaseConfiguration(string name,
            int expirationProcessTimerInSeconds,
            bool enableFullTextSearch,
            TimeSpan auditRetentionPeriod,
            int maxBodySizeToStore,
            int minimumStorageLeftRequiredForIngestion,
            ServerConfiguration serverConfiguration)
        {
            Name = name;
            ExpirationProcessTimerInSeconds = expirationProcessTimerInSeconds;
            EnableFullTextSearch = enableFullTextSearch;
            AuditRetentionPeriod = auditRetentionPeriod;
            MaxBodySizeToStore = maxBodySizeToStore;
            ServerConfiguration = serverConfiguration;
            MinimumStorageLeftRequiredForIngestion = minimumStorageLeftRequiredForIngestion;
        }

        public string Name { get; }

        public int ExpirationProcessTimerInSeconds { get; }

        public bool EnableFullTextSearch { get; }

        public Func<string, BlittableJsonReaderObject, string> FindClrType { get; }

        public ServerConfiguration ServerConfiguration { get; }

        public TimeSpan AuditRetentionPeriod { get; }

        public int MaxBodySizeToStore { get; }

        public int MinimumStorageLeftRequiredForIngestion { get; internal set; } //Setting for ATT only
    }
}
