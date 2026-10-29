namespace ServiceControl.Persistence.EFCore.PostgreSql;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Infrastructure;

class PostgreSqlPersistence(PostgreSqlPersisterSettings settings) : BasePersistence, IPersistence
{
    public void AddPersistence(IServiceCollection services)
    {
        RegisterSettings(services);
        ConfigureDbContext(services);
        RegisterDataStores(services, settings);

        services.AddSingleton<IFailedMessageIngestionSqlDialect, PostgreSqlFailedMessageIngestionSqlDialect>();
        services.AddSingleton<IRetryBatchSqlDialect, PostgreSqlRetryBatchSqlDialect>();
        services.AddSingleton<IFullTextSearchDialect, PostgreSqlFullTextSearchDialect>();
        services.AddSingleton<IEndpointThroughputDialect, PostgreSqlEndpointThroughputDialect>();
        services.AddSingleton<IDatabaseHostingProbe, PostgreSqlDatabaseHostingProbe>();
        services.AddSingleton<IStorageIdentityProvider, PostgreSqlStorageIdentityProvider>();
        services.AddSingleton<IStorageFootprintProbe, PostgreSqlStorageFootprintProbe>();
    }

    public void AddInstaller(IServiceCollection services)
    {
        RegisterSettings(services);
        ConfigureDbContext(services);

        services.AddScoped<IDatabaseMigrator, PostgreSqlDatabaseMigrator>();
        RegisterBodyStorageInstaller(services, settings);
    }

    void RegisterSettings(IServiceCollection services)
    {
        services.AddSingleton<PersistenceSettings>(settings);
        services.AddSingleton<EFPersisterSettings>(settings);
        services.AddSingleton(settings);
    }

    void ConfigureDbContext(IServiceCollection services)
    {
        services.AddDbContext<PostgreSqlServiceControlDbContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(settings.ConnectionString, npgsqlOptions =>
            {
                npgsqlOptions.CommandTimeout(settings.CommandTimeout);

                npgsqlOptions.MigrationsHistoryTable(ServiceControlDbContext.MigrationsHistoryTableName, settings.Schema);

                if (settings.EnableRetryOnFailure)
                {
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: settings.MaxRetryCount,
                        maxRetryDelay: TimeSpan.FromSeconds(settings.MaxRetryDelayInSeconds),
                        errorCodesToAdd: null);
                }
            });

            if (settings.Schema is not null)
            {
                ((IDbContextOptionsBuilderInfrastructure)options).AddOrUpdateExtension(new SchemaOptionsExtension(settings.Schema));
                options.ReplaceService<IMigrationsSqlGenerator, SchemaStampingNpgsqlMigrationsSqlGenerator>();
                options.ReplaceService<IModelCacheKeyFactory, SchemaModelCacheKeyFactory>();

                // Schema stamping intentionally differs from the schema-less snapshot; PendingModelChangesTests checks migration drift.
                options.ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
            }

            if (settings.EnableSensitiveDataLogging)
            {
                options.EnableSensitiveDataLogging();
            }
        }, ServiceLifetime.Scoped);

        services.AddScoped<ServiceControlDbContext>(provider => provider.GetRequiredService<PostgreSqlServiceControlDbContext>());
    }
}
