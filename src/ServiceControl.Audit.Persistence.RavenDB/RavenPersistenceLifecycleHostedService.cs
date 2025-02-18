namespace ServiceControl.Audit.Persistence.RavenDB
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;

    sealed class RavenPersistenceLifecycleHostedService(IRavenPersistenceLifecycle lifecycle) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) => lifecycle.Initialize(cancellationToken);

        public Task StopAsync(CancellationToken cancellationToken) => lifecycle.Stop(cancellationToken);
    }
}