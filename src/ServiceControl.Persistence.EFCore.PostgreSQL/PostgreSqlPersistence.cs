namespace ServiceControl.Persistence.EFCore.PostgreSql;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Persistence.EFCore.Abstractions;

class PostgreSqlPersistence(PostgreSqlPersisterSettings settings) : BasePersistence, IPersistence
{
    public void AddPersistence(IServiceCollection services)
    {
        RegisterSettings(services);
        ConfigureDbContext(services);
        RegisterDataStores(services);
    }

    public void AddInstaller(IServiceCollection services)
    {
        RegisterSettings(services);
        ConfigureDbContext(services);
    }

    void RegisterSettings(IServiceCollection services)
    {
        services.AddSingleton<PersistenceSettings>(settings);
        services.AddSingleton<EFPersisterSettings>(settings);
        services.AddSingleton(settings);
    }

    void ConfigureDbContext(IServiceCollection services) =>
        services.AddPooledDbContextFactory<PostgreSqlServiceControlDbContext>(options =>
            options.UseNpgsql(settings.ConnectionString, npgsql => npgsql.CommandTimeout(settings.CommandTimeout)));
}
