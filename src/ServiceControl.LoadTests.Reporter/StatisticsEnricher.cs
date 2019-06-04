namespace ServiceControl.LoadTests.Reporter
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Audit.Auditing;
    using Metrics;
    using NServiceBus;

    class StatisticsEnricher : AuditImportEnricher
    {
        public StatisticsEnricher(Statistics statistics, Meter processedMeter)
        {
            this.statistics = statistics;
            this.processedMeter = processedMeter;
        }

        public override Task Enrich(IReadOnlyDictionary<string, string> headers, IDictionary<string, object> metadata)
        {
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