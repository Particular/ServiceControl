namespace ServiceControl.Audit.Persistence.PostgreSQL
{
    using System;

    class DatabaseConfiguration(
        string databaseName,
        int expirationProcessTimerInSeconds,
        TimeSpan auditRetentionPeriod,
        int maxBodySizeToStore,
        string connectionString)
    {
        public string Name { get; } = databaseName;

        public int ExpirationProcessTimerInSeconds { get; } = expirationProcessTimerInSeconds;

        public TimeSpan AuditRetentionPeriod { get; } = auditRetentionPeriod;

        public int MaxBodySizeToStore { get; } = maxBodySizeToStore;
        public string ConnectionString { get; } = connectionString;
    }
}
