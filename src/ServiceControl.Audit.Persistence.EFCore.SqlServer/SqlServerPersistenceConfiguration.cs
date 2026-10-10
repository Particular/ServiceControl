namespace ServiceControl.Audit.Persistence.EFCore.SqlServer;

using ServiceControl.Audit.Persistence.EFCore.Abstractions;

class SqlServerPersistenceConfiguration : EFPersistenceConfigurationBase
{
    public override string Name => "SQLServer";

    protected override IPersistence Create(EFPersisterSettings settings) => new SqlServerPersistence(settings);
}
