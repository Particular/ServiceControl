namespace Particular.ThroughputCollector.Persistence
{
    using System;

    public class EndpointThroughput
    {
        public DateTime DateUTC { get; set; }
        public long TotalThroughput { get; set; }
    }
}
