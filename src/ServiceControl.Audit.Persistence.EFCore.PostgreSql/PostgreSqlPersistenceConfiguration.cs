namespace ServiceControl.Audit.Persistence.EFCore.PostgreSql;

using ServiceControl.Audit.Persistence.EFCore.Abstractions;

class PostgreSqlPersistenceConfiguration : EFPersistenceConfigurationBase
{
    public override string Name => "PostgreSQL";

    public static readonly TimeSpan MinimumRetentionPeriod = TimeSpan.FromDays(1);
    public static readonly TimeSpan MaximumRetentionPeriod = TimeSpan.FromDays(90);

    protected override IPersistence Create(EFPersisterSettings settings)
    {
        if (settings.AuditRetentionPeriod < MinimumRetentionPeriod || settings.AuditRetentionPeriod > MaximumRetentionPeriod)
        {
            throw new InvalidOperationException(
                $"AuditRetentionPeriod is {settings.AuditRetentionPeriod}, but the PostgreSQL persister keeps audit data for 1 to 90 days. Set AuditRetentionPeriod within that range.");
        }

        return new PostgreSqlPersistence(settings);
    }
}
