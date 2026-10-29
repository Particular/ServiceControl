namespace ServiceControl.Persistence.EFCore.SqlServer;

using ServiceControl.Persistence.EFCore.Abstractions;

class SqlServerPersistenceConfiguration : EFPersistenceConfigurationBase
{
    public override IPersistence Create(PersistenceSettings settings)
    {
        var sqlSettings = (SqlServerPersisterSettings)settings;

        return new SqlServerPersistence(sqlSettings);
    }

    protected override EFPersisterSettings CreateSettings(string connectionString, BodyStorageSettings bodyStorage) =>
        new SqlServerPersisterSettings { ConnectionString = connectionString, BodyStorage = bodyStorage };
}
