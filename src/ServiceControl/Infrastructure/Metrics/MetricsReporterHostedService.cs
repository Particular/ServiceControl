namespace ServiceControl.Infrastructure.Metrics
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    class MetricsReporterHostedService : IHostedService
    {
        readonly Metrics metrics;
        readonly ILogger<Metrics> logger;

        MetricsReporter reporter;

        public MetricsReporterHostedService(Metrics metrics, ILogger<Metrics> logger)
        {
            this.metrics = metrics;
            this.logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            reporter = new MetricsReporter(metrics, x => logger.LogInformation(x), TimeSpan.FromSeconds(5));

            reporter.Start();

            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                await reporter.Stop();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                //NOOP
            }
        }
    }
}
