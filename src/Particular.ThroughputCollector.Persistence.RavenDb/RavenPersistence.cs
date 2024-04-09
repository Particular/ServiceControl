namespace Particular.ThroughputCollector.Persistence.RavenDb;

using Microsoft.Extensions.DependencyInjection;
using Raven.Client.Documents;

class RavenPersistence(DatabaseConfiguration databaseConfiguration) : IPersistence
{
    public IServiceCollection Configure(IServiceCollection services)
    {
        services.AddSingleton(databaseConfiguration);
        services.AddSingleton<IThroughputDataStore, ThroughputDataStore>();
        services.AddSingleton<IPersistenceInstaller, RavenInstaller>(provider =>
        {
            var store = provider.GetRequiredService<Lazy<IDocumentStore>>();

            return new RavenInstaller(store, databaseConfiguration);
        });

        return services;
    }
}