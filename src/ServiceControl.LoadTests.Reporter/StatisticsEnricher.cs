namespace ServiceControl.LoadTests.Reporter
{
    using System.Threading.Tasks;
    using Audit.Auditing;
    using Metrics;
    using NServiceBus;

    class StatisticsEnricher : IEnrichImportedAuditMessages
    {
        public StatisticsEnricher(Statistics statistics, Meter processedMeter)
        {
            this.statistics = statistics;
            this.processedMeter = processedMeter;
        }

        public Task Enrich(AuditEnricherContext context)
        {
            var headers = context.Headers;

            if (!headers.ContainsKey("NServiceBus.FailedQ"))
            {
                statistics.AuditReceived(headers[Headers.HostId]);
                processedMeter.Mark();
            }

            return Task.CompletedTask;
        }

        Statistics statistics;
        Meter processedMeter;
    }
}