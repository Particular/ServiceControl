namespace Particular.ThroughputCollector.Persistence.RavenDb;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Raven.Client.Documents;
using Raven.Client.ServerWide;
using Raven.Client.ServerWide.Operations;

class RavenInstaller(IServiceProvider provider, DatabaseConfiguration databaseConfiguration) : IPersistenceInstaller
{
    public async Task Install(CancellationToken cancellationToken)
    {
        var store = provider.GetRequiredService<IDocumentStore>();

        var record = await store.Maintenance.Server
            .SendAsync(new GetDatabaseRecordOperation(databaseConfiguration.Name), cancellationToken)
            .ConfigureAwait(false);

        if (record == null)
        {
            await store.Maintenance.Server
                .SendAsync(new CreateDatabaseOperation(new DatabaseRecord(databaseConfiguration.Name)), cancellationToken)
                .ConfigureAwait(false);
        }
    }
}