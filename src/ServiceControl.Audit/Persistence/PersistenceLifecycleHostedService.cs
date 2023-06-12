namespace ServiceControl.Audit.Persistence
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;

    class PersistenceLifecycleHostedService : IHostedService
    {
        public PersistenceLifecycleHostedService(IPersistenceLifecycle lifecycle, IHostApplicationLifetime hostApplicationLifetime)
        {
            this.lifecycle = lifecycle;
            this.hostApplicationLifetime = hostApplicationLifetime;
        }

        public Task StartAsync(CancellationToken cancellationToken) =>
            lifecycle.Start(() =>
            {
                hostApplicationLifetime.StopApplication();
            }, cancellationToken);

        public Task StopAsync(CancellationToken cancellationToken) => lifecycle.Stop(cancellationToken);

        readonly IHostApplicationLifetime hostApplicationLifetime;
        readonly IPersistenceLifecycle lifecycle;
    }
}