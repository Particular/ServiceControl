namespace Particular.ThroughputCollector.Persistence.InMemory;

using System.Threading;
using System.Threading.Tasks;

class InMemoryPersistenceLifecycle : IPersistenceLifecycle
{
    public Task Start(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
    public Task Stop(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
