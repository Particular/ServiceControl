namespace Particular.License.Throughput.Audit
{
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Particular.License.Contracts;

    class AuditThroughputCollectorHostedService : IHostedService
    {
        public AuditThroughputCollectorHostedService(ILoggerFactory loggerFactory, PlatformData platformData)
        {
            logger = loggerFactory.CreateLogger<AuditThroughputCollectorHostedService>();
            this.platformData = platformData;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Starting AuditThroughputCollector Service");
            auditThroughputGatherTimer = new Timer(_ => GatherThroughput(), null, TimeSpan.Zero, TimeSpan.FromMinutes(1)); //TODO this will change to less often
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Stopping AuditThroughputCollector Service");
            auditThroughputGatherTimer?.Dispose();
            return Task.CompletedTask;
        }

        void GatherThroughput()
        {
            logger.LogInformation($"Gathering throughput from audit");
        }

        readonly ILogger logger;
#pragma warning disable IDE0052 // Remove unread private members
        PlatformData platformData;
        Timer? auditThroughputGatherTimer;
#pragma warning restore IDE0052 // Remove unread private members
    }
}
