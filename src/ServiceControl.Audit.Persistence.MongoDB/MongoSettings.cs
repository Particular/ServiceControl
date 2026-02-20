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
        string blobConnectionString = null,
        string blobContainerName = "message-bodies",
        int bodyWriterBatchSize = 500,
        int bodyWriterParallelWriters = 4,
        TimeSpan? bodyWriterBatchTimeout = null)
    {
        public string ConnectionString { get; } = connectionString;
        public string DatabaseName { get; } = databaseName;
        public TimeSpan AuditRetentionPeriod { get; } = auditRetentionPeriod;
        public bool EnableFullTextSearchOnBodies { get; } = enableFullTextSearchOnBodies;
        public int MaxBodySizeToStore { get; } = maxBodySizeToStore;
        public BodyStorageType BodyStorageType { get; } = bodyStorageType;
        public string BlobConnectionString { get; } = blobConnectionString;
        public string BlobContainerName { get; } = blobContainerName;
        public int BodyWriterBatchSize { get; } = bodyWriterBatchSize;
        public int BodyWriterParallelWriters { get; } = bodyWriterParallelWriters;
        public TimeSpan BodyWriterBatchTimeout { get; } = bodyWriterBatchTimeout ?? TimeSpan.FromMilliseconds(500);
    }
}
