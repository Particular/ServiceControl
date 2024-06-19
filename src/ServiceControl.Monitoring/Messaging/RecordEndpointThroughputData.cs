namespace NServiceBus.Metrics
{
    using System;

    public class RecordEndpointThroughputData : ICommand
    {
        public EndpointThroughputData[] EndpointThroughputData { get; set; }
        public DateTime StartDateTime { get; set; }
        public DateTime EndDateTime { get; set; }
    }

    public class EndpointThroughputData
    {
        public string Name { get; set; }
        public long Throughput { get; set; }
    }
}