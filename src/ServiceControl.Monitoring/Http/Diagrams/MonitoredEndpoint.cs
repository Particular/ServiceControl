namespace ServiceControl.Monitoring.Http.Diagrams
{
    using System.Collections.Generic;

    public class MonitoredEndpoint
    {
        public string Name { get; set; }
        public bool IsStale { get; set; }
        public string[] EndpointInstanceIds { get; set; }
        public Dictionary<string, MonitoredValues> Metrics { get; } = new Dictionary<string, MonitoredValues>();
    }
}