namespace Particular.ThroughputCollector.Persistence.RavenDb;

using Microsoft.Extensions.DependencyInjection;
using Raven.Client.Documents;
using Raven.Client.ServerWide;
using Raven.Client.ServerWide.Operations;

class RavenInstaller(IServiceProvider provider, DatabaseConfiguration databaseConfiguration) : IPersistenceInstaller
{
    public async Task Install(CancellationToken cancellationToken)
    {
        Lazy<IDocumentStore> store = provider.GetRequiredService<Lazy<IDocumentStore>>();

        DatabaseRecordWithEtag? record = await store.Value.Maintenance.Server
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