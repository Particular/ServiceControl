namespace ServiceControl.Persistence.Sql.PostgreSQL;

using Core.Abstractions;
using Core.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Persistence;

class PostgreSqlPersistence : BasePersistence, IPersistence
{
    readonly PostgreSqlPersisterSettings settings;

    public PostgreSqlPersistence(PostgreSqlPersisterSettings settings)
    {
        this.settings = settings;
    }

    public void AddPersistence(IServiceCollection services)
    {
        ConfigureDbContext(services);
        RegisterDataStores(services);
    }

    public void AddInstaller(IServiceCollection services)
    {
        ConfigureDbContext(services);
        RegisterDataStores(services);

        services.AddSingleton<IDatabaseMigrator, PostgreSqlDatabaseMigrator>();
    }

    void ConfigureDbContext(IServiceCollection services)
    {
        services.AddSingleton<PersistenceSettings>(settings);
        services.AddSingleton(settings);


        services.AddDbContext<PostgreSqlDbContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(settings.ConnectionString, npgsqlOptions =>
            {
                npgsqlOptions.CommandTimeout(settings.CommandTimeout);
                if (settings.EnableRetryOnFailure)
                {
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorCodesToAdd: null);
                }
            });

            if (settings.EnableSensitiveDataLogging)
            {
                options.EnableSensitiveDataLogging();
            }
        }, ServiceLifetime.Scoped);

        services.AddScoped<ServiceControlDbContextBase>(sp => sp.GetRequiredService<PostgreSqlDbContext>());
    }
}
