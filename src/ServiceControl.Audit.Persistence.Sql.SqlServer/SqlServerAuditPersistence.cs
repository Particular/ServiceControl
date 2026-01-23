namespace ServiceControl.Audit.Persistence.Sql.SqlServer;

using Core.Abstractions;
using Core.DbContexts;
using Core.FullTextSearch;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

class SqlServerAuditPersistence : BaseAuditPersistence, IPersistence
{
    readonly SqlServerAuditPersisterSettings settings;

    public SqlServerAuditPersistence(SqlServerAuditPersisterSettings settings)
    {
        this.settings = settings;
    }

    public void AddPersistence(IServiceCollection services)
    {
        ConfigureDbContext(services);
        RegisterDataStores(services);
        services.AddSingleton<IAuditFullTextSearchProvider, SqlServerAuditFullTextSearchProvider>();
    }

    public void AddInstaller(IServiceCollection services)
    {
        ConfigureDbContext(services);
        RegisterDataStores(services);
        services.AddSingleton<IAuditFullTextSearchProvider, SqlServerAuditFullTextSearchProvider>();
        services.AddSingleton<IAuditDatabaseMigrator, SqlServerAuditDatabaseMigrator>();
    }

    void ConfigureDbContext(IServiceCollection services)
    {
        services.AddSingleton<PersistenceSettings>(settings);
        services.AddSingleton<AuditSqlPersisterSettings>(settings);
        services.AddSingleton(settings);

        services.AddDbContext<SqlServerAuditDbContext>((serviceProvider, options) =>
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

        services.AddScoped<AuditDbContextBase>(sp => sp.GetRequiredService<SqlServerAuditDbContext>());
    }
}
