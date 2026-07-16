namespace ServiceControl.Persistence.EFCore.PostgreSql;

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

        return new PostgreSqlPersistence((PostgreSqlPersisterSettings)settings);
    }

    protected override EFPersisterSettings CreateSettings(string connectionString) =>
        new PostgreSqlPersisterSettings { ConnectionString = connectionString };
}
