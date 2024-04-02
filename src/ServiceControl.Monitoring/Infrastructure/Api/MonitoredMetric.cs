namespace ServiceControl.Monitoring.Infrastructure.Api
{
    using System;
    using ServiceControl.Monitoring.Http.Diagrams;
    using System.Collections.Generic;

    class MonitoredMetric<T>
    {
        public Type StoreType { get; set; }
        public string ReturnName { get; set; }
        public Aggregation<T> Aggregate { get; set; }
    }

    delegate MonitoredValues Aggregation<T>(List<IntervalsStore<T>.IntervalsBreakdown> intervals, HistoryPeriod period);
}
