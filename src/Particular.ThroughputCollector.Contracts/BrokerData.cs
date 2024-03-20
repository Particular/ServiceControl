namespace Particular.ThroughputCollector.Contracts;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

public record BrokerData
{
    public Broker Broker { get; set; }
    public string ScopeType { get; set; }
    public string Version { get; set; }
}