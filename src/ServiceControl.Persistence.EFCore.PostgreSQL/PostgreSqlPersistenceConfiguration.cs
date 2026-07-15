namespace ServiceControl.Persistence.EFCore.PostgreSQL;

using ServiceControl.Persistence.EFCore.Abstractions;

class PostgreSqlPersistenceConfiguration : EFPersistenceConfigurationBase
{
    public override IPersistence Create(PersistenceSettings settings) =>
        new PostgreSqlPersistence((PostgreSqlPersisterSettings)settings);

    protected override EFPersisterSettings CreateSettings(string connectionString) =>
        new PostgreSqlPersisterSettings { ConnectionString = connectionString };
}
