namespace ServiceControl.Audit.Persistence.Sql.PostgreSQL;

using Core.Abstractions;

public class PostgreSqlAuditPersisterSettings : AuditSqlPersisterSettings
{
    public PostgreSqlAuditPersisterSettings(
        TimeSpan auditRetentionPeriod,
        bool enableFullTextSearchOnBodies,
        int maxBodySizeToStore)
        : base(auditRetentionPeriod, enableFullTextSearchOnBodies, maxBodySizeToStore)
    {
    }

    public bool EnableRetryOnFailure { get; set; } = true;
}
