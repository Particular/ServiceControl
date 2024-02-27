namespace Particular.ThroughputCollector.Persistence.InMemory;

using System.Threading;
using System.Threading.Tasks;

class InMemoryPersistenceLifecycle : PersistenceService
{
    public override Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public override Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
