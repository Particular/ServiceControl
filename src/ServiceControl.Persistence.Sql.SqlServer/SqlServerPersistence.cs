namespace ServiceControl.Persistence.Sql.SqlServer;

using Core.Abstractions;
using Core.DbContexts;
using Core.FullTextSearch;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Persistence;

class SqlServerPersistence : BasePersistence, IPersistence
{
    readonly SqlServerPersisterSettings settings;

    public SqlServerPersistence(SqlServerPersisterSettings settings)
    {
        this.settings = settings;
    }

    public void AddPersistence(IServiceCollection services)
    {
        ConfigureDbContext(services);
        RegisterDataStores(services);
        services.AddSingleton<IFullTextSearchProvider, SqlServerFullTextSearchProvider>();
    }

    public void AddInstaller(IServiceCollection services)
    {
        ConfigureDbContext(services);
        RegisterDataStores(services);
        services.AddSingleton<IFullTextSearchProvider, SqlServerFullTextSearchProvider>();

        // Register the database migrator - this runs during installation/setup
        services.AddSingleton<IDatabaseMigrator, SqlServerDatabaseMigrator>();
    }

    void ConfigureDbContext(IServiceCollection services)
    {
        services.AddSingleton<PersistenceSettings>(settings);
        services.AddSingleton(settings);

        services.AddDbContext<SqlServerDbContext>((serviceProvider, options) =>
        {
            options.UseSqlServer(settings.ConnectionString, sqlOptions =>
            {
                sqlOptions.CommandTimeout(settings.CommandTimeout);
                if (settings.EnableRetryOnFailure)
                {
                    sqlOptions.EnableRetryOnFailure(
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

        // Register as base type for TrialLicenseDataProvider
        services.AddScoped<ServiceControlDbContextBase>(sp => sp.GetRequiredService<SqlServerDbContext>());
    }
}
