namespace Particular.ThroughputCollector.Persistence
{
    public class EndpointThroughput
    {
        public DateOnly DateUTC { get; set; }
        public long TotalThroughput { get; set; }
    }
}
