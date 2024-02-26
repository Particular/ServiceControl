namespace Particular.ThroughputCollector.Persistence
{
    using System;

    public class EndpointThroughput
    {
        public DateTime DateUTC { get; set; }
        public int TotalThroughput { get; set; }
    }
}
