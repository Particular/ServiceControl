namespace ServiceControl.Audit.Persistence.EFCore.PostgreSql;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus;
using ServiceControl.Audit.Persistence.EFCore.Abstractions;
using ServiceControl.Audit.Persistence.EFCore.DbContexts;
using ServiceControl.Audit.Persistence.EFCore.Infrastructure;

class PostgreSqlPersistence(EFPersisterSettings settings) : EFPersistenceBase(settings)
{
    protected override void AddDbContext(IServiceCollection services, EFPersisterSettings settings)
    {
        services.AddDbContext<PostgreSqlAuditDbContext>(options =>
        {
            options.UseNpgsql(settings.ConnectionString, npgsqlOptions =>
            {
                npgsqlOptions.CommandTimeout(settings.CommandTimeout);
                npgsqlOptions.MigrationsHistoryTable(AuditDbContext.MigrationsHistoryTableName, settings.Schema);

                if (settings.EnableRetryOnFailure)
                {
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: settings.MaxRetryCount,
                        maxRetryDelay: TimeSpan.FromSeconds(settings.MaxRetryDelayInSeconds),
                        errorCodesToAdd: null);
                }
            });

            options.ReplaceService<IMigrationsSqlGenerator, AuditNpgsqlMigrationsSqlGenerator>();

            if (settings.Schema is not null)
            {
                ((IDbContextOptionsBuilderInfrastructure)options).AddOrUpdateExtension(new SchemaOptionsExtension(settings.Schema));
                options.ReplaceService<IModelCacheKeyFactory, SchemaModelCacheKeyFactory>();

                // HasDefaultSchema makes the model differ from the migrations snapshot on purpose.
                options.ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
            }

            if (settings.EnableSensitiveDataLogging)
            {
                options.EnableSensitiveDataLogging();
            }
        }, ServiceLifetime.Scoped);

        services.AddScoped<AuditDbContext>(provider => provider.GetRequiredService<PostgreSqlAuditDbContext>());
    }

    protected override void AddPartitionManager(IServiceCollection services) =>
        services.AddSingleton<IAuditPartitionManager, PostgreSqlAuditPartitionManager>();

    protected override void AddQueryServices(IServiceCollection services)
    {
        services.AddSingleton<IFullTextSearchDialect, PostgreSqlFullTextSearchDialect>();
        services.AddSingleton<IRetentionLock, PostgreSqlRetentionLock>();
    }

    protected override void AddCustomChecks(EndpointConfiguration endpointConfiguration) =>
        endpointConfiguration.AddCustomCheck<AuditPartitionCustomCheck>();
}
