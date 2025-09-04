namespace ServiceControl.Audit.Persistence.PostgreSQL;

using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using ServiceControl.Audit.Auditing.BodyStorage;
using ServiceControl.Audit.Persistence;
using ServiceControl.Audit.Persistence.PostgreSQL.BodyStorage;
using ServiceControl.Audit.Persistence.PostgreSQL.UnitOfWork;
using ServiceControl.Audit.Persistence.UnitOfWork;
using Npgsql;

class PostgreSQLPersistence(DatabaseConfiguration databaseConfiguration) : IPersistence
{
    public void AddInstaller(IServiceCollection services)
    {
        services.AddSingleton(databaseConfiguration);
        services.AddSingleton<PostgreSQLConnectionFactory>();
        services.AddHostedService<PostgreSQLPersistenceInstaller>();
    }

    public void AddPersistence(IServiceCollection services)
    {
        services.AddSingleton(databaseConfiguration);
        services.AddSingleton<IAuditDataStore, PostgreSQLAuditDataStore>();
        services.AddSingleton<IAuditIngestionUnitOfWorkFactory, PostgreSQLAuditIngestionUnitOfWorkFactory>();
        services.AddSingleton<IFailedAuditStorage, PostgreSQLFailedAuditStorage>();
        services.AddSingleton<IBodyStorage, PostgreSQLAttachmentsBodyStorage>();
        services.AddSingleton<PostgreSQLConnectionFactory>();
        services.AddHostedService<RetentionCleanupService>();
        services.ConfigureOpenTelemetryMeterProvider(b => b.AddNpgsqlInstrumentation());
    }
}
