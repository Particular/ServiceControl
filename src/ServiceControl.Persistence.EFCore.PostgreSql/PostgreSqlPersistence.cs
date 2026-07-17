namespace ServiceControl.Persistence.EFCore.PostgreSql;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.PostgreSql.Infrastructure;

class PostgreSqlPersistence(PostgreSqlPersisterSettings settings) : BasePersistence, IPersistence
{
    public void AddPersistence(IServiceCollection services)
    {
        RegisterSettings(services);
        ConfigureDbContext(services);
        RegisterDataStores(services);

        services.AddHostedService<KnownEndpointsReconciler>();
    }

    public void AddInstaller(IServiceCollection services)
    {
        RegisterSettings(services);
        ConfigureDbContext(services);

        services.AddScoped<IDatabaseMigrator, PostgreSqlDatabaseMigrator>();
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
                if (settings.EnableRetryOnFailure)
                {
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: settings.MaxRetryCount,
                        maxRetryDelay: TimeSpan.FromSeconds(settings.MaxRetryDelayInSeconds),
                        errorCodesToAdd: null);
                }
            });

            if (settings.EnableSensitiveDataLogging)
            {
                options.EnableSensitiveDataLogging();
            }
        }, ServiceLifetime.Scoped);

        services.AddScoped<ServiceControlDbContext>(provider => provider.GetRequiredService<PostgreSqlServiceControlDbContext>());
    }
}
