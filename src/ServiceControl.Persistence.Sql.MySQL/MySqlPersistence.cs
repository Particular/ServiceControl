namespace ServiceControl.Persistence.Sql.MySQL;

using Core.Abstractions;
using Core.DbContexts;
using Core.FullTextSearch;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Persistence;

class MySqlPersistence : BasePersistence, IPersistence
{
    readonly MySqlPersisterSettings settings;

    public MySqlPersistence(MySqlPersisterSettings settings)
    {
        this.settings = settings;
    }

    public void AddPersistence(IServiceCollection services)
    {
        ConfigureDbContext(services);
        RegisterDataStores(services);
        services.AddSingleton<IFullTextSearchProvider, MySqlFullTextSearchProvider>();
    }

    public void AddInstaller(IServiceCollection services)
    {
        ConfigureDbContext(services);
        RegisterDataStores(services);
        services.AddSingleton<IFullTextSearchProvider, MySqlFullTextSearchProvider>();
        services.AddSingleton<IDatabaseMigrator, MySqlDatabaseMigrator>();
    }

    void ConfigureDbContext(IServiceCollection services)
    {
        services.AddSingleton<PersistenceSettings>(settings);
        services.AddSingleton(settings);

        services.AddDbContext<MySqlDbContext>((serviceProvider, options) =>
        {
            options.UseMySql(settings.ConnectionString, ServerVersion.AutoDetect(settings.ConnectionString), mySqlOptions =>
            {
                mySqlOptions.CommandTimeout(settings.CommandTimeout);
                if (settings.EnableRetryOnFailure)
                {
                    mySqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null);
                }
            });

            if (settings.EnableSensitiveDataLogging)
            {
                options.EnableSensitiveDataLogging();
            }
        }, ServiceLifetime.Scoped);

        services.AddScoped<ServiceControlDbContextBase>(sp => sp.GetRequiredService<MySqlDbContext>());
    }
}
