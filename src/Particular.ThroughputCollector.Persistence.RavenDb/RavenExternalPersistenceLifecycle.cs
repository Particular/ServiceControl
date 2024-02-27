namespace Particular.ThroughputCollector.Persistence.RavenDb;

using System;
using System.Threading;
using System.Threading.Tasks;
using Raven.Client.Documents;
using Raven.Client.Documents.Conventions;

class RavenExternalPersistenceLifecycle(DatabaseConfiguration configuration) : PersistenceService, IRavenDocumentStoreProvider
{
    public IDocumentStore GetDocumentStore()
    {
        if (documentStore == null)
        {
            throw new InvalidOperationException("Document store is not available until the persistence have been started");
        }

        return documentStore;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        var store = new DocumentStore
        {
            Database = configuration.Name,
            Urls = new[] { configuration.ServerConfiguration.ConnectionString },
            Conventions = new DocumentConventions
            {
                SaveEnumsAsIntegers = true
            }
        };

        documentStore = store.Initialize();

        var databaseSetup = new DatabaseSetup(configuration);
        await databaseSetup.Execute(store, cancellationToken).ConfigureAwait(false);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        documentStore?.Dispose();

        return Task.CompletedTask;
    }

    IDocumentStore? documentStore;
}