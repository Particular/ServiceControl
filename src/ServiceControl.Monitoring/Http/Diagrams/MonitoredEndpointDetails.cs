namespace ServiceControl.Monitoring.Http.Diagrams
{
    public class MonitoredEndpointDetails
    {
        public MonitoredEndpointDigest Digest { get; set; }
        public MonitoredEndpointInstance[] Instances { get; set; }
        public MonitoredEndpointMessageType[] MessageTypes { get; set; }
        public MonitoredEndpointMetricDetails MetricDetails { get; set; }
    }
}