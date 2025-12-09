namespace ServiceControl.Persistence.Sql.Core.Entities;

public class ThroughputEndpointEntity
{
    public int Id { get; set; }
    public required string EndpointName { get; set; }
    public required string ThroughputSource { get; set; }
    public string? SanitizedEndpointName { get; set; }
    public string? EndpointIndicators { get; set; }
    public string? UserIndicator { get; set; }
    public string? Scope { get; set; }
    public DateOnly LastCollectedData { get; set; }
}
