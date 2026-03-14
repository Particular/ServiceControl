namespace ServiceControl.Audit.Persistence.Sql.Core.Abstractions;

public interface IAuditDatabaseMigrator
{
    Task ApplyMigrations(CancellationToken cancellationToken = default);
}
