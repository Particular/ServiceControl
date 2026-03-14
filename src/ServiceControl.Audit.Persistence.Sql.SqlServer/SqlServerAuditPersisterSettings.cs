namespace ServiceControl.Audit.Persistence.Sql.SqlServer;

using Core.Abstractions;

public class SqlServerAuditPersisterSettings : AuditSqlPersisterSettings
{
    public SqlServerAuditPersisterSettings(
        TimeSpan auditRetentionPeriod,
        bool enableFullTextSearchOnBodies,
        int maxBodySizeToStore)
        : base(auditRetentionPeriod, enableFullTextSearchOnBodies, maxBodySizeToStore)
    {
    }

    public bool EnableRetryOnFailure { get; set; } = true;
}
