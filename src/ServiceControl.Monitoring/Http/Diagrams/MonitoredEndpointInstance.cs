namespace ServiceControl.Monitoring.Http.Diagrams
{
    using System.Collections.Generic;

    public class MonitoredEndpointInstance
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public bool IsStale { get; set; }
        public Dictionary<string, MonitoredValues> Metrics { get; } = new Dictionary<string, MonitoredValues>();
    }
}