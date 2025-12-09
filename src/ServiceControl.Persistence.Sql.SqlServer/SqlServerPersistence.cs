namespace ServiceControl.Persistence.Sql.SqlServer;

using Core.Abstractions;
using Core.DbContexts;
using Core.Implementation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Particular.LicensingComponent.Persistence;
using ServiceControl.Persistence;

class SqlServerPersistence : IPersistence
{
    readonly SqlServerPersisterSettings settings;

    public SqlServerPersistence(SqlServerPersisterSettings settings)
    {
        this.settings = settings;
    }

    public void AddPersistence(IServiceCollection services)
    {
        ConfigureDbContext(services);

        if (settings.MaintenanceMode)
        {
            return;
        }

        services.AddSingleton<ITrialLicenseDataProvider, TrialLicenseDataProvider>();
        services.AddSingleton<ILicensingDataStore, LicensingDataStore>();
    }

    public void AddInstaller(IServiceCollection services)
    {
        ConfigureDbContext(services);

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
