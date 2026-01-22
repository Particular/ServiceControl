namespace ServiceControl.Audit.Persistence.Sql.Core.Abstractions;

public abstract class AuditSqlPersisterSettings : PersistenceSettings
{
    protected AuditSqlPersisterSettings(
        TimeSpan auditRetentionPeriod,
        bool enableFullTextSearchOnBodies,
        int maxBodySizeToStore)
        : base(auditRetentionPeriod, enableFullTextSearchOnBodies, maxBodySizeToStore)
    {
    }

    public required string ConnectionString { get; set; }
    public int CommandTimeout { get; set; } = 30;
    public bool EnableSensitiveDataLogging { get; set; } = false;
    public required string MessageBodyStoragePath { get; set; }
    public int MinBodySizeForCompression { get; set; } = 4096;
}
