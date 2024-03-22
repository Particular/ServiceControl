namespace ServiceControl.Audit.Persistence.RavenDB
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;

    class RavenInstaller(IPersistenceLifecycle persistenceLifecycle, IHostApplicationLifetime lifetime) : IHostedService
    {
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await persistenceLifecycle.Initialize(cancellationToken);
            lifetime.StopApplication();
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}