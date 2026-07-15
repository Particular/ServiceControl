namespace ServiceControl.Persistence.EFCore.SqlServer;

using ServiceControl.Persistence.EFCore.Abstractions;

class SqlServerPersistenceConfiguration : EFPersistenceConfigurationBase
{
    public override IPersistence Create(PersistenceSettings settings) =>
        new SqlServerPersistence((SqlServerPersisterSettings)settings);

    protected override EFPersisterSettings CreateSettings(string connectionString) =>
        new SqlServerPersisterSettings { ConnectionString = connectionString };
}
