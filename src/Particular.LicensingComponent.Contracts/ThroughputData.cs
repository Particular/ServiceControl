namespace Particular.LicensingComponent.Contracts;

public class ThroughputData : SortedList<DateOnly, long>
{
    public ThroughputData()
    {
    }

    public ThroughputData(IEnumerable<EndpointDailyThroughput> throughput)
        : base(throughput.ToDictionary(entry => entry.DateUTC, entry => entry.MessageCount))
    {
    }

    public ThroughputSource ThroughputSource { get; set; }
}