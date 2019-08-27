namespace ServiceControl.Monitoring.Http.Diagrams
{
    using System.Collections.Generic;

    public class MonitoredEndpointDigest
    {
        public Dictionary<string, MonitoredEndpointMetricDigest> Metrics { get; set; } = new Dictionary<string, MonitoredEndpointMetricDigest>();
    }
}