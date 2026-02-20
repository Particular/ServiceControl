namespace ServiceControl.Audit.Persistence.MongoDB
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    sealed class MongoPersistenceLifecycleHostedService(
        IMongoPersistenceLifecycle lifecycle,
        ILogger<MongoPersistenceLifecycleHostedService> logger) : IHostedService
    {
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                await lifecycle.Initialize(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                logger.LogInformation("MongoDB persistence initialization cancelled");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => lifecycle.Stop(cancellationToken);
    }
}
