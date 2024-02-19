namespace Throughput.Persistence.RavenDb;

using System.Threading;
using System.Threading.Tasks;

class RavenInstaller(IRavenPersistenceLifecycle lifecycle) : IPersistenceInstaller
{
    public async Task Install(CancellationToken cancellationToken)
    {
        await lifecycle.Start(cancellationToken).ConfigureAwait(false);
        await lifecycle.Stop(cancellationToken).ConfigureAwait(false);
    }
}