namespace ServiceControl.Audit.Persistence
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;

    class PersistenceLifecycleHostedService(IPersistenceLifecycle lifecycle) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) => lifecycle.Initialize(cancellationToken);

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}