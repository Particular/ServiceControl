using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using ServiceControl.Persistence;


sealed class PersisterInitializingLifetimeWrapper : IHostLifetime
{
    readonly IPersistenceLifecycle persistenceLifecycle;
    readonly IHostLifetime implementation;

    public PersisterInitializingLifetimeWrapper(
        IPersistenceLifecycle persistenceLifecycle,
        IHostLifetime implementation
        )
    {
        this.persistenceLifecycle = persistenceLifecycle;
        this.implementation = implementation;
    }

    public async Task WaitForStartAsync(CancellationToken cancellationToken)
    {
        await implementation.WaitForStartAsync(cancellationToken);
        await persistenceLifecycle.Initialize(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => implementation.StopAsync(cancellationToken);
}