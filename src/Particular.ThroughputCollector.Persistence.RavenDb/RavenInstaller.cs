namespace Particular.ThroughputCollector.Persistence.RavenDb;

using System.Threading;
using System.Threading.Tasks;
using Raven.Client.Documents;
using Raven.Client.ServerWide;
using Raven.Client.ServerWide.Operations;

class RavenInstaller(Lazy<IDocumentStore> store, DatabaseConfiguration databaseConfiguration) : IPersistenceInstaller
{
    public async Task Install(CancellationToken cancellationToken)
    {
        var record = await store.Value.Maintenance.Server
            .SendAsync(new GetDatabaseRecordOperation(databaseConfiguration.Name), cancellationToken)
            .ConfigureAwait(false);

        if (record == null)
        {
            await store.Value.Maintenance.Server
                .SendAsync(new CreateDatabaseOperation(new DatabaseRecord(databaseConfiguration.Name)), cancellationToken)
                .ConfigureAwait(false);
        }
    }
}