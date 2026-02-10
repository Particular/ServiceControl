namespace ServiceControl.Persistence.Sql.Core.Abstractions;

using ServiceControl.Persistence;

public abstract class SqlPersisterSettings : PersistenceSettings
{
    public required string ConnectionString { get; set; }
    public int CommandTimeout { get; set; } = 30;
    public bool EnableSensitiveDataLogging { get; set; } = false;
}
