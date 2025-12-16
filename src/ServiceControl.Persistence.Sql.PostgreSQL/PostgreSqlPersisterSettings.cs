namespace ServiceControl.Persistence.Sql.PostgreSQL;

using Core.Abstractions;

public class PostgreSqlPersisterSettings : SqlPersisterSettings
{
    public bool EnableRetryOnFailure { get; set; } = true;
}
