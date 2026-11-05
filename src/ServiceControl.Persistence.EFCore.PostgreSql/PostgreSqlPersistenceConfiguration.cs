namespace ServiceControl.Persistence.EFCore.PostgreSql;

using ServiceControl.Persistence.EFCore.Abstractions;

class PostgreSqlPersistenceConfiguration : EFPersistenceConfigurationBase
{
    public override IPersistence Create(PersistenceSettings settings)
    {
        return new PostgreSqlPersistence((PostgreSqlPersisterSettings)settings);
    }

    protected override EFPersisterSettings CreateSettings(string connectionString, BodyStorageSettings bodyStorage) =>
        new PostgreSqlPersisterSettings { ConnectionString = connectionString, BodyStorage = bodyStorage };
}
