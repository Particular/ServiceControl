namespace ServiceControl.Monitoring.Http.Diagrams
{
    using System.Collections.Generic;

    public class MonitoredEndpointMessageType
    {
        public string Id { get; set; }

        public string TypeName { get; set; }

        public string AssemblyName { get; set; }

        public string AssemblyVersion { get; set; }

        public string Culture { get; set; }

        public string PublicKeyToken { get; set; }

        public Dictionary<string, MonitoredValues> Metrics { get; } = new Dictionary<string, MonitoredValues>();
    }
}