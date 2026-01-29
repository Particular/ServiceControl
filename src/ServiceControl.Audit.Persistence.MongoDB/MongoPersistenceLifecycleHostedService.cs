namespace ServiceControl.Audit.Persistence.MongoDB
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;

    /// <summary>
    /// Hosted service that manages the MongoDB persistence lifecycle.
    /// </summary>
    sealed class MongoPersistenceLifecycleHostedService(IMongoPersistenceLifecycle lifecycle) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) => lifecycle.Initialize(cancellationToken);

        public Task StopAsync(CancellationToken cancellationToken) => lifecycle.Stop(cancellationToken);
    }
}
