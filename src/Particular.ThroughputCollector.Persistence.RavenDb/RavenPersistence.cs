namespace Particular.ThroughputCollector.Persistence.RavenDb;

using Microsoft.Extensions.DependencyInjection;
//using Persistence.UnitOfWork;
//using RavenDB.CustomChecks;
//using UnitOfWork;

class RavenPersistence(DatabaseConfiguration databaseConfiguration) : IPersistence
{
    public PersistenceService Configure(IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton(databaseConfiguration);
        serviceCollection.AddSingleton<IRavenSessionProvider, RavenSessionProvider>();
        serviceCollection.AddSingleton<IThroughputDataStore, ThroughputDataStore>();
        //serviceCollection.AddSingleton<IAuditIngestionUnitOfWorkFactory, RavenAuditIngestionUnitOfWorkFactory>();
        //serviceCollection.AddSingleton<IFailedAuditStorage, RavenFailedAuditStorage>();
        //serviceCollection.AddSingleton<CheckMinimumStorageRequiredForAuditIngestion.State>();

        var persistenceService = CreateService();

        if (persistenceService is IRavenDocumentStoreProvider provider)
        {
            serviceCollection.AddSingleton(_ => provider);
        }

        return persistenceService;
    }

    public IPersistenceInstaller CreateInstaller() => new RavenInstaller(CreateService());

    PersistenceService CreateService()
    {
        var serverConfiguration = databaseConfiguration.ServerConfiguration;

        if (serverConfiguration.UseEmbeddedServer)
        {
            return new RavenEmbeddedPersistenceLifecycle(databaseConfiguration);
        }

        return new RavenExternalPersistenceLifecycle(databaseConfiguration);
    }
}