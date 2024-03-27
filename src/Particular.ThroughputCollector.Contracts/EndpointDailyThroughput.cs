namespace Particular.ThroughputCollector.Contracts;
public class EndpointDailyThroughput
{
    public DateOnly DateUTC { get; set; }
    public long TotalThroughput { get; set; }
}
