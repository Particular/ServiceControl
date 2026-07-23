namespace ServiceControl.Persistence.EFCore.SqlServer;

using CustomChecks;
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

        var sqlSettings = (SqlServerPersisterSettings)settings;

        CheckFreeDiskSpace.Validate(sqlSettings);
        CheckMinimumStorageRequiredForIngestion.Validate(sqlSettings);

        return new SqlServerPersistence(sqlSettings);
    }

    protected override EFPersisterSettings CreateSettings(string connectionString, BodyStorageSettings bodyStorage) =>
        new SqlServerPersisterSettings { ConnectionString = connectionString, BodyStorage = bodyStorage };
}
