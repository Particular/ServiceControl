namespace Particular.ThroughputCollector.Persistence.RavenDb;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

class RavenInstaller(IHostedService persistenceService) : IPersistenceInstaller
{
    public async Task Install(CancellationToken cancellationToken)
    {
        await persistenceService.StartAsync(cancellationToken).ConfigureAwait(false);
        await persistenceService.StopAsync(cancellationToken).ConfigureAwait(false);
    }
}