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
        services.AddSingleton<IDatabaseHostingProbe, PostgreSqlDatabaseHostingProbe>();
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

                if (settings.Schema is not null)
                {
                    // Its own history table per schema, so schemas sharing a database migrate
                    // independently. Without this EF leaves the history table unqualified and every
                    // schema reads the same one.
                    npgsqlOptions.MigrationsHistoryTable(HistoryRepository.DefaultTableName, settings.Schema);
                }

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

                // HasDefaultSchema moves every table, so the model is meant to differ from the
                // snapshot the migrations were scaffolded against. A default installation keeps the
                // check, where a difference really would mean a migration is missing.
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
