namespace ServiceControl.Audit.Persistence.PostgreSQL;

using System;
class DatabaseConfiguration(
    string databaseName,
    string adminDatabaseName,
    int expirationProcessTimerInSeconds,
    TimeSpan auditRetentionPeriod,
    int maxBodySizeToStore,
    string connectionString)
{
    public string Name { get; } = databaseName;
    public string AdminDatabaseName { get; } = adminDatabaseName;
    public int ExpirationProcessTimerInSeconds { get; } = expirationProcessTimerInSeconds;

    public TimeSpan AuditRetentionPeriod { get; } = auditRetentionPeriod;

    public int MaxBodySizeToStore { get; } = maxBodySizeToStore;
    public string ConnectionString { get; } = connectionString;
}
