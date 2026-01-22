namespace ServiceControl.Audit.Persistence.Sql.Core.Abstractions;

public interface IAuditDatabaseMigrator
{
    Task Migrate(CancellationToken cancellationToken = default);
}
