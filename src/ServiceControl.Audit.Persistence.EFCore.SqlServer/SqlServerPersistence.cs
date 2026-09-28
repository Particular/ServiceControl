namespace ServiceControl.Audit.Persistence.EFCore.SqlServer;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Audit.Persistence.EFCore.Abstractions;
using ServiceControl.Audit.Persistence.EFCore.DbContexts;
using ServiceControl.Audit.Persistence.EFCore.Infrastructure;

class SqlServerPersistence(EFPersisterSettings settings) : EFPersistenceBase(settings)
{
    protected override void AddDbContext(IServiceCollection services, EFPersisterSettings settings)
    {
        services.AddDbContext<SqlServerAuditDbContext>(options =>
        {
            options.UseSqlServer(settings.ConnectionString, sqlOptions =>
            {
                sqlOptions.CommandTimeout(settings.CommandTimeout);
                sqlOptions.MigrationsHistoryTable(AuditDbContext.MigrationsHistoryTableName, settings.Schema);

                if (settings.EnableRetryOnFailure)
                {
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: settings.MaxRetryCount,
                        maxRetryDelay: TimeSpan.FromSeconds(settings.MaxRetryDelayInSeconds),
                        errorNumbersToAdd: null);
                }
            });

            if (settings.Schema is not null)
            {
                ((IDbContextOptionsBuilderInfrastructure)options).AddOrUpdateExtension(new SchemaOptionsExtension(settings.Schema));
                options.ReplaceService<IMigrationsSqlGenerator, SchemaStampingSqlServerMigrationsSqlGenerator>();
                options.ReplaceService<IModelCacheKeyFactory, SchemaModelCacheKeyFactory>();

                // HasDefaultSchema makes the model differ from the migrations snapshot on purpose.
                options.ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
            }

            if (settings.EnableSensitiveDataLogging)
            {
                options.EnableSensitiveDataLogging();
            }
        }, ServiceLifetime.Scoped);

        services.AddScoped<AuditDbContext>(provider => provider.GetRequiredService<SqlServerAuditDbContext>());
    }

    protected override void AddPartitionManager(IServiceCollection services) =>
        services.AddSingleton<IAuditPartitionManager, SqlServerAuditPartitionManager>();

    protected override void AddQueryServices(IServiceCollection services)
    {
        services.AddSingleton<IFullTextSearchDialect, SqlServerFullTextSearchDialect>();
        services.AddSingleton<IRetentionLock, SqlServerRetentionLock>();
    }
}
