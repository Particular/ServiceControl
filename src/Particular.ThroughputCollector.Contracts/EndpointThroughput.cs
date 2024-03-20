namespace Particular.ThroughputCollector.Contracts;
public class EndpointThroughput
{
    public DateOnly DateUTC { get; set; }
    public long TotalThroughput { get; set; }
}
