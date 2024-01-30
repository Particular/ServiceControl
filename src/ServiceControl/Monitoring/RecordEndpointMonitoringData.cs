namespace ServiceControl.Monitoring
{
    using System;
    using NServiceBus;

    public class RecordEndpointMonitoringData : ICommand
    {
        public EndpointMonitoringData[] EndpointMonitoringData { get; set; }
        public DateTime StartDateTime { get; set; }
        public DateTime EndDateTime { get; set; }
    }

    public class EndpointMonitoringData
    {
        public string Name { get; set; }
        public string[] EndpointInstanceIds { get; set; } = [];
        public string Metrics { get; set; }
    }
}