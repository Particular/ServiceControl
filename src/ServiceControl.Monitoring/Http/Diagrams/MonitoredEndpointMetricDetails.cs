namespace ServiceControl.Monitoring.Http.Diagrams
{
    using System.Collections.Generic;

    public class MonitoredEndpointMetricDetails
    {
        public Dictionary<string, MonitoredValuesWithTimings> Metrics { get; set; } = new Dictionary<string, MonitoredValuesWithTimings>();
    }
}