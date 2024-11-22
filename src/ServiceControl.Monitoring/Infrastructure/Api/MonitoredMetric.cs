namespace ServiceControl.Monitoring.Infrastructure.Api
{
    using System;
    using System.Collections.Generic;
    using ServiceControl.Monitoring.Http.Diagrams;

    class MonitoredMetric<T>
    {
        public Type StoreType { get; set; }
        public string ReturnName { get; set; }
        public Aggregation<T> Aggregate { get; set; }
    }

    delegate MonitoredValues Aggregation<T>(List<IntervalsStore<T>.IntervalsBreakdown> intervals, HistoryPeriod period);
}