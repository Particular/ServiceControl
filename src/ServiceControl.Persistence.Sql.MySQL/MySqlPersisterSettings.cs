namespace ServiceControl.Persistence.Sql.MySQL;

using Core.Abstractions;

public class MySqlPersisterSettings : SqlPersisterSettings
{
    public bool EnableRetryOnFailure { get; set; } = true;
}
