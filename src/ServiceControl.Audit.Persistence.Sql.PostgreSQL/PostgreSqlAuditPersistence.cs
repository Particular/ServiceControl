namespace ServiceControl.Audit.Persistence.Sql.PostgreSQL;

using Core.Abstractions;
using Core.DbContexts;
using Core.FullTextSearch;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

class PostgreSqlAuditPersistence : BaseAuditPersistence, IPersistence
{
    readonly PostgreSqlAuditPersisterSettings settings;

    public PostgreSqlAuditPersistence(PostgreSqlAuditPersisterSettings settings)
    {
        this.settings = settings;
    }

    public void AddPersistence(IServiceCollection services)
    {
        ConfigureDbContext(services);
        RegisterDataStores(services, settings);
        services.AddSingleton<IAuditFullTextSearchProvider, PostgreSqlAuditFullTextSearchProvider>();
        services.AddHostedService<KnownEndpointsReconciler>();
    }

    public void AddInstaller(IServiceCollection services)
    {
        ConfigureDbContext(services);
        RegisterDataStores(services, settings);
        services.AddSingleton<IAuditFullTextSearchProvider, PostgreSqlAuditFullTextSearchProvider>();
        services.AddSingleton<IAuditDatabaseMigrator, PostgreSqlAuditDatabaseMigrator>();
    }

    void ConfigureDbContext(IServiceCollection services)
    {
        services.AddSingleton<PersistenceSettings>(settings);
        services.AddSingleton<AuditSqlPersisterSettings>(settings);
        services.AddSingleton(settings);

        services.AddDbContext<PostgreSqlAuditDbContext>((serviceProvider, options) =>
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

        services.AddScoped<AuditDbContextBase>(sp => sp.GetRequiredService<PostgreSqlAuditDbContext>());
    }
}
