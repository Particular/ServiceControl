namespace Throughput.Persistence.RavenDb;

using System;

public class DatabaseConfiguration(string name,
    int expirationProcessTimerInSeconds,
    bool enableFullTextSearch,
    TimeSpan auditRetentionPeriod,
    int maxBodySizeToStore,
    int minimumStorageLeftRequiredForIngestion,
    ServerConfiguration serverConfiguration)
{
    public string Name { get; } = name;

    public int ExpirationProcessTimerInSeconds { get; } = expirationProcessTimerInSeconds;

    public bool EnableFullTextSearch { get; } = enableFullTextSearch;

    public ServerConfiguration ServerConfiguration { get; } = serverConfiguration;

    public TimeSpan AuditRetentionPeriod { get; } = auditRetentionPeriod;

    public int MaxBodySizeToStore { get; } = maxBodySizeToStore;

    public int MinimumStorageLeftRequiredForIngestion { get; internal set; } = minimumStorageLeftRequiredForIngestion;
}
