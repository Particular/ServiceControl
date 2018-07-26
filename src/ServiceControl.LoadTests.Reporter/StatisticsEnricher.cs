namespace ServiceControl.LoadTests.Reporter
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NServiceBus;
    using Operations;

    class StatisticsEnricher : ImportEnricher
    {
        Statistics statistics;

        public StatisticsEnricher(Statistics statistics)
        {
            this.statistics = statistics;
        }

        public override Task Enrich(IReadOnlyDictionary<string, string> headers, IDictionary<string, object> metadata)
        {
            if (!headers.ContainsKey("NServiceBus.FailedQ"))
            {
                statistics.AuditReceived(headers[Headers.HostId]);
            }
            return Task.CompletedTask;
        }
    }
}