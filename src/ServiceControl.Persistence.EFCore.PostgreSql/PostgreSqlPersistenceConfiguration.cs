namespace ServiceControl.Persistence.EFCore.PostgreSql;

using CustomChecks;
using Microsoft.Extensions.Logging;
using ServiceControl.Infrastructure;
using ServiceControl.Persistence.EFCore.Abstractions;

class PostgreSqlPersistenceConfiguration : EFPersistenceConfigurationBase
{
    public override IPersistence Create(PersistenceSettings settings)
    {
        // Temporary until the persister is fully implemented
        LoggerUtil.CreateStaticLogger<PostgreSqlPersistenceConfiguration>()
            .LogError("The PostgreSQL persistence is still under development and is not ready for use");

        var postgresSettings = (PostgreSqlPersisterSettings)settings;

        CheckFreeDiskSpace.Validate(postgresSettings);
        CheckMinimumStorageRequiredForIngestion.Validate(postgresSettings);

        return new PostgreSqlPersistence(postgresSettings);
    }

    protected override EFPersisterSettings CreateSettings(string connectionString, BodyStorageSettings bodyStorage) =>
        new PostgreSqlPersisterSettings { ConnectionString = connectionString, BodyStorage = bodyStorage };
}
