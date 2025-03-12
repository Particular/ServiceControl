#nullable enable

namespace ServiceControl.Persistence.RavenDB
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;

    class RavenPersistenceLifecycleHostedService(IRavenPersistenceLifecycle persistenceLifecycle) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) => persistenceLifecycle.Initialize(cancellationToken);

        public Task StopAsync(CancellationToken cancellationToken) => persistenceLifecycle.Stop(cancellationToken);
    }
}