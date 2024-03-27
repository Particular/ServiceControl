namespace Particular.ThroughputCollector.Persistence.RavenDb;

using Microsoft.Extensions.DependencyInjection;

class RavenPersistence(DatabaseConfiguration databaseConfiguration) : IPersistence
{
    public IServiceCollection Configure(IServiceCollection services)
    {
        services.AddSingleton(databaseConfiguration);
        services.AddSingleton<IThroughputDataStore, ThroughputDataStore>();
        services.AddSingleton<IPersistenceInstaller, RavenInstaller>(provider => new RavenInstaller(provider, databaseConfiguration));

        //serviceCollection.AddSingleton<IAuditIngestionUnitOfWorkFactory, RavenAuditIngestionUnitOfWorkFactory>();
        //serviceCollection.AddSingleton<IFailedAuditStorage, RavenFailedAuditStorage>();
        //serviceCollection.AddSingleton<CheckMinimumStorageRequiredForAuditIngestion.State>();

        return services;
    }
}