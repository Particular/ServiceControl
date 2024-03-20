namespace Particular.ThroughputCollector.Contracts;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

public record Endpoint
{
    public string Name { get; set; }
    public string SanitizedName { get; set; }
    public ThroughputSource ThroughputSource { get; set; }
    public string[] EndpointIndicators { get; set; }
    public string UserIndicator { get; set; }
    public string? Scope { get; set; }
    public List<EndpointThroughput> DailyThroughput { get; set; } = [];
}