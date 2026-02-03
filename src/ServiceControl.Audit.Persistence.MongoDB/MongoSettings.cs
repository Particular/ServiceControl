namespace ServiceControl.Audit.Persistence.MongoDB
{
    using System;

    public class MongoSettings(
        string connectionString,
        string databaseName,
        TimeSpan auditRetentionPeriod,
        bool enableFullTextSearchOnBodies,
        int maxBodySizeToStore,
        BodyStorageType bodyStorageType = BodyStorageType.Database,
        string bodyStoragePath = null)
    {
        public string ConnectionString { get; } = connectionString;
        public string DatabaseName { get; } = databaseName;
        public TimeSpan AuditRetentionPeriod { get; } = auditRetentionPeriod;
        public bool EnableFullTextSearchOnBodies { get; } = enableFullTextSearchOnBodies;
        public int MaxBodySizeToStore { get; } = maxBodySizeToStore;
        public BodyStorageType BodyStorageType { get; } = bodyStorageType;
        public string BodyStoragePath { get; } = bodyStoragePath;
    }
}
