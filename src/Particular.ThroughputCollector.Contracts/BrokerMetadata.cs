namespace Particular.ThroughputCollector.Contracts;

public record BrokerMetadata(string? ScopeType, Dictionary<string, string> Data)
{
}