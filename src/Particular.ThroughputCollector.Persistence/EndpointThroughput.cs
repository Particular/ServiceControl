namespace Particular.ThroughputCollector.Persistence
{
    public class EndpointThroughput
    {
        public DateTime DateUTC { get; set; }
        public long TotalThroughput { get; set; }
        public string[] EndpointIndicators { get; set; } = [];
        public string? Scope { get; set; }
    }
}
