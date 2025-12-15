namespace ServiceControl.Persistence.Sql.Core.Abstractions;

public interface IDatabaseMigrator
{
    Task ApplyMigrations(CancellationToken cancellationToken = default);
}
