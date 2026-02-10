namespace ServiceControl.Persistence.Sql.SqlServer;

using Core.Abstractions;

public class SqlServerPersisterSettings : SqlPersisterSettings
{
    public bool EnableRetryOnFailure { get; set; } = true;
}
