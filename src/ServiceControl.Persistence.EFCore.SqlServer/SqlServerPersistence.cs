namespace ServiceControl.Persistence.EFCore.SqlServer;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Infrastructure;

class SqlServerPersistence(SqlServerPersisterSettings settings) : BasePersistence, IPersistence
{
    public void AddPersistence(IServiceCollection services)
    {
        RegisterSettings(services);
        ConfigureDbContext(services);
        RegisterDataStores(services, settings);

        services.AddSingleton<IFailedMessageIngestionSqlDialect, SqlServerFailedMessageIngestionSqlDialect>();
        services.AddSingleton<IRetryBatchSqlDialect, SqlServerRetryBatchSqlDialect>();
        services.AddSingleton<IFullTextSearchDialect, SqlServerFullTextSearchDialect>();
        services.AddSingleton<IEndpointThroughputDialect, SqlServerEndpointThroughputDialect>();
        services.AddSingleton<IDatabaseHostingProbe, SqlServerDatabaseHostingProbe>();
        services.AddSingleton<IStorageIdentityProvider, SqlServerStorageIdentityProvider>();
        services.AddSingleton<IStorageFootprintProbe, SqlServerStorageFootprintProbe>();
    }

    public void AddInstaller(IServiceCollection services)
    {
        RegisterSettings(services);
        ConfigureDbContext(services);

        services.AddScoped<IDatabaseMigrator, SqlServerDatabaseMigrator>();
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
        services.AddDbContext<SqlServerServiceControlDbContext>((serviceProvider, options) =>
        {
            options.UseSqlServer(settings.ConnectionString, sqlOptions =>
            {
                sqlOptions.CommandTimeout(settings.CommandTimeout);

                sqlOptions.MigrationsHistoryTable(ServiceControlDbContext.MigrationsHistoryTableName, settings.Schema);

                if (settings.EnableRetryOnFailure)
                {
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: settings.MaxRetryCount,
                        maxRetryDelay: TimeSpan.FromSeconds(settings.MaxRetryDelayInSeconds),
                        errorNumbersToAdd: FullTextDaemonErrorNumbers);
                }
            });

            if (settings.Schema is not null)
            {
                ((IDbContextOptionsBuilderInfrastructure)options).AddOrUpdateExtension(new SchemaOptionsExtension(settings.Schema));
                options.ReplaceService<IMigrationsSqlGenerator, SchemaStampingSqlServerMigrationsSqlGenerator>();
                options.ReplaceService<IModelCacheKeyFactory, SchemaModelCacheKeyFactory>();

                // Schema stamping intentionally differs from the schema-less snapshot; PendingModelChangesTests checks migration drift.
                options.ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
            }

            if (settings.EnableSensitiveDataLogging)
            {
                options.EnableSensitiveDataLogging();
            }
        }, ServiceLifetime.Scoped);

        services.AddScoped<ServiceControlDbContext>(provider => provider.GetRequiredService<SqlServerServiceControlDbContext>());
    }

    // Azure SQL raises these while its full-text daemon is unavailable, which happens on a new database and now and then after.
    static readonly int[] FullTextDaemonErrorNumbers = [30046, 30053];
}
