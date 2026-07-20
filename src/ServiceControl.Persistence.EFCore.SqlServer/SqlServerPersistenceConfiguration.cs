namespace ServiceControl.Persistence.EFCore.SqlServer;

using Microsoft.Extensions.Logging;
using ServiceControl.Infrastructure;
using ServiceControl.Persistence.EFCore.Abstractions;

class SqlServerPersistenceConfiguration : EFPersistenceConfigurationBase
{
    public override IPersistence Create(PersistenceSettings settings)
    {
        // Temporary until the persister is fully implemented
        LoggerUtil.CreateStaticLogger<SqlServerPersistenceConfiguration>()
            .LogError("The SQL Server persistence is still under development and is not ready for use");

        return new SqlServerPersistence((SqlServerPersisterSettings)settings);
    }

    protected override EFPersisterSettings CreateSettings(string connectionString) =>
        new SqlServerPersisterSettings { ConnectionString = connectionString };
}
