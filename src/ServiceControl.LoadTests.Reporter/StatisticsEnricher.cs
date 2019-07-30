namespace ServiceControl.LoadTests.Reporter
{
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

        public void Enrich(AuditEnricherContext context)
        {
            var headers = context.Headers;

            if (!headers.ContainsKey("NServiceBus.FailedQ"))
            {
                statistics.AuditReceived(headers[Headers.HostId]);
                processedMeter.Mark();
            }
        }

        Statistics statistics;
        Meter processedMeter;
    }
}