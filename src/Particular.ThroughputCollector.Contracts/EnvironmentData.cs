namespace Particular.ThroughputCollector.Contracts;

public record EnvironmentData
{
    public string? ScopeType { get; set; }
    public Dictionary<string, string> Data { get; set; } = [];
}