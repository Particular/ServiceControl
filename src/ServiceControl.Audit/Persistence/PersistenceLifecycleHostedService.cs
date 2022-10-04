namespace ServiceControl.Audit.Persistence
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;

    class PersistenceLifecycleHostedService : IHostedService
    {
        public PersistenceLifecycleHostedService(IPersistenceLifecycle lifecycle) => this.lifecycle = lifecycle;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return lifecycle.Start(cancellationToken);
        }
        public Task StopAsync(CancellationToken cancellationToken)
        {
            return lifecycle.Stop(cancellationToken);
        }

        readonly IPersistenceLifecycle lifecycle;
    }
}