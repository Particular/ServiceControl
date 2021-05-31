namespace ServiceControl.Infrastructure.Metrics
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using NServiceBus.Logging;

    class MetricsReporterHostedService : IHostedService
    {
        readonly Metrics metrics;
        MetricsReporter reporter;

        public MetricsReporterHostedService(Metrics metrics) => this.metrics = metrics;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            var metricsLog = LogManager.GetLogger("Metrics");

            reporter = new MetricsReporter(metrics, x => metricsLog.Info(x), TimeSpan.FromSeconds(5));

            reporter.Start();

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => reporter.Stop();
    }
}
