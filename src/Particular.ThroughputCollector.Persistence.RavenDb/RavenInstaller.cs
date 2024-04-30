namespace Particular.ThroughputCollector.Persistence.RavenDb;

using Microsoft.Extensions.DependencyInjection;
using Raven.Client.Documents;
using Raven.Client.ServerWide;
using Raven.Client.ServerWide.Operations;

class RavenInstaller(IServiceProvider provider) : IPersistenceInstaller
{
    public async Task Install(CancellationToken cancellationToken)
    {
        var store = provider.GetRequiredService<Lazy<IDocumentStore>>();
        var databaseConfiguration = provider.GetRequiredService<DatabaseConfiguration>();

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