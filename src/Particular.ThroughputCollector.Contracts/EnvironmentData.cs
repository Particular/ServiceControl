namespace Particular.ThroughputCollector.Contracts;

public record EnvironmentData
{
    public string? ScopeType { get; set; }
    public Dictionary<string, string> Data { get; set; } = [];
    public AuditInstance[] AuditInstances { get; set; } = [];
}

public record AuditInstance
{
    public string Url { get; set; } = "";
    public string? Version { get; set; }
    public string? MessageTransport { get; set; }
}