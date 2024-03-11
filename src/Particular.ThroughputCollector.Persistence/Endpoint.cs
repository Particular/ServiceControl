namespace Particular.ThroughputCollector.Persistence;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

public record Endpoint
{
    public string Name { get; set; }
    public string SanitizedName { get; set; }
    public ThroughputSource ThroughputSource { get; set; }
    public string[] EndpointIndicators { get; set; }
    public bool? UserIndicatedSendOnly { get; set; }
    public bool? UserIndicatedToIgnore { get; set; }

    public List<EndpointThroughput> DailyThroughput { get; set; } = [];
}