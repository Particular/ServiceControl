namespace Particular.ThroughputCollector.Contracts;

public record AuditServiceMetadata(Dictionary<string, int> Versions, Dictionary<string, int> Transports)
{
}